#!/usr/bin/env bash
# bin/codex-wave.sh — fan an entire dependency-wave out across parallel worktrees.
#
# Creates one git worktree per task in the wave (off main), runs Codex on each in
# parallel (headless `codex exec`, capped at JOBS), gates each with bin/verify.sh +
# bin/check-test-integrity.sh, and reports PASS/FAIL. Nothing is merged — review the
# PASS branches, merge them to main, THEN run the next wave.
#
# PREREQUISITE: the previous wave must already be merged into main, because every task
# branches off main (or $BASE). Run waves in order 0 -> 1 -> ... (see docs/codex/WAVES.md).
#
# Usage:
#   bin/codex-wave.sh 1               # run wave 1 (tasks 14 15 17 18) in parallel
#   JOBS=2 bin/codex-wave.sh 2        # cap concurrent Codex runs (default 3)
#   BASE=codex/integration bin/codex-wave.sh 1   # branch off a different base
#   DRY_RUN=1 bin/codex-wave.sh 1     # print the plan, run nothing
#
# Requires: codex, dotnet, npm, docker (running), git. Each parallel run is HEAVY
# (~8-10 GB RAM + its own Testcontainers Postgres during gating); keep JOBS modest.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"; cd "$ROOT" || exit 1

WAVE="${1:-}"
[ -z "$WAVE" ] && { echo "usage: bin/codex-wave.sh <wave 0..6>  (see docs/codex/WAVES.md)" >&2; exit 2; }

wave_tasks() {
  case "$1" in
    0) echo "13 11" ;;
    1) echo "14 15 17 18" ;;
    2) echo "16 20 12 09" ;;
    3) echo "19 21 22" ;;
    4) echo "23" ;;
    5) echo "24" ;;
    6) echo "10" ;;
    *) echo "" ;;
  esac
}
effort_for() { case "$1" in 14|16|17) echo ultra;; 12) echo xhigh;; 09|22|10) echo medium;; *) echo high;; esac; }

MODEL="${CODEX_MODEL:-gpt-5.6-sol}"
BASE="${BASE:-main}"
JOBS="${JOBS:-3}"
DRY_RUN="${DRY_RUN:-0}"
TASKS="$(wave_tasks "$WAVE")"
[ -z "$TASKS" ] && { echo "unknown wave: $WAVE (valid 0..6)" >&2; exit 2; }
mkdir -p reports || { echo "cannot create reports directory" >&2; exit 2; }
RUN_SUFFIX=""
[ "$DRY_RUN" = 1 ] && RUN_SUFFIX="-dry-run"
WLOG="reports/codex-wave-${WAVE}${RUN_SUFFIX}.md"

command -v git >/dev/null || { echo "git required" >&2; exit 2; }
case "$JOBS" in
  ''|*[!0-9]*) echo "JOBS must be a positive integer" >&2; exit 2 ;;
esac
[ "$JOBS" -gt 0 ] || { echo "JOBS must be a positive integer" >&2; exit 2; }
BASE_COMMIT="$(git rev-parse --verify "${BASE}^{commit}" 2>/dev/null)" \
  || { echo "base does not resolve to a commit: $BASE" >&2; exit 2; }
if [ "$DRY_RUN" != 1 ]; then
  command -v codex  >/dev/null || { echo "codex not on PATH" >&2; exit 2; }
  command -v dotnet >/dev/null || { echo "dotnet not on PATH" >&2; exit 2; }
  docker info >/dev/null 2>&1  || { echo "Docker must be running (integration tests need it)." >&2; exit 2; }
fi

spec_for() { ls docs/tasks/task-"$1"-*.md 2>/dev/null | head -1 || true; }
render()    { sed -e "s#{{TASK_NUMBER}}#$1#g" -e "s#{{SPEC_PATH}}#$2#g" docs/codex/kickoff-prompt-template.md; }

run_one() {
  local NN spec eff wt br log st prompt codex_rc task_head
  NN="$(printf '%02d' "$((10#$1))")"
  spec="$(spec_for "$NN")"; eff="$(effort_for "$NN")"
  wt="../tradebook-wt/task-${NN}"; br="codex/task-${NN}"
  log="reports/codex-wave-${WAVE}-task-${NN}${RUN_SUFFIX}.log"
  st="reports/codex-wave-${WAVE}-task-${NN}${RUN_SUFFIX}.status"
  if [ "$DRY_RUN" != 1 ] && ! : >"$log"; then
    echo "LOG-FAIL" >"$st" 2>/dev/null || true
    return
  fi
  if [ -z "$spec" ]; then echo "task spec not found" >>"$log"; echo "NO-SPEC" >"$st"; return; fi
  if [ "$DRY_RUN" = 1 ]; then
    echo "[dry] task $NN  effort=$eff  worktree=$wt  branch=$br off $BASE ($BASE_COMMIT)  spec=$spec"
    echo "DRY-RUN" >"$st"; return
  fi
  if [ -e "$wt" ] || [ -L "$wt" ]; then
    echo "refusing existing worktree path: $wt" >>"$log"
    echo "WORKTREE-EXISTS" >"$st"; return
  fi
  if git show-ref --verify --quiet "refs/heads/$br"; then
    echo "refusing existing branch: $br" >>"$log"
    echo "BRANCH-EXISTS" >"$st"; return
  fi
  git worktree add -b "$br" "$wt" "$BASE_COMMIT" >>"$log" 2>&1 \
    || { echo "WORKTREE-FAIL" >"$st"; return; }
  prompt="$(render "$NN" "$spec")" \
    || { echo "prompt rendering failed" >>"$log"; echo "PROMPT-FAIL" >"$st"; return; }
  if ( cd "$wt" && codex exec --model "$MODEL" -c "model_reasoning_effort=${eff}" \
        --sandbox workspace-write -c sandbox_workspace_write.network_access=true \
        -c approval_policy=never "$prompt" ) >>"$log" 2>&1; then
    codex_rc=0
  else
    codex_rc=$?
  fi
  if [ "$codex_rc" -ne 0 ]; then
    echo "codex exec failed with exit code $codex_rc; gates were not run" >>"$log"
    echo "CODEX-FAIL" >"$st"; return
  fi
  if ! git -C "$wt" merge-base --is-ancestor "$BASE_COMMIT" HEAD; then
    echo "task branch no longer descends from pinned base $BASE_COMMIT" >>"$log"
    echo "HISTORY-FAIL" >"$st"; return
  fi
  if [ "$(git -C "$wt" rev-list --count "$BASE_COMMIT..HEAD")" -eq 0 ] \
      || git -C "$wt" diff --quiet "$BASE_COMMIT" HEAD --; then
    echo "codex produced no committed tree change from $BASE_COMMIT" >>"$log"
    echo "NO-COMMITTED-CHANGE" >"$st"; return
  fi
  if [ -n "$(git -C "$wt" status --porcelain)" ]; then
    echo "codex left uncommitted changes; refusing to gate an unreproducible branch" >>"$log"
    echo "UNCOMMITTED" >"$st"; return
  fi
  task_head="$(git -C "$wt" rev-parse HEAD)"
  if ( cd "$wt" && bin/verify.sh && bin/check-test-integrity.sh "$BASE_COMMIT" ) >>"$log" 2>&1; then
    if [ "$(git -C "$wt" rev-parse HEAD)" != "$task_head" ] \
        || [ -n "$(git -C "$wt" status --porcelain)" ]; then
      echo "gates changed the committed or working tree; PASS would not be reproducible" >>"$log"
      echo "GATE-DIRTY" >"$st"
    else
      echo "PASS" >"$st"
    fi
  else
    echo "FAIL" >"$st"
  fi
}

echo "# Codex wave ${WAVE}: ${TASKS}" | tee "$WLOG"
echo "" | tee -a "$WLOG"
echo "Base: \`${BASE}\` (\`${BASE_COMMIT}\`) · JOBS=${JOBS} · model ${MODEL}. Prior waves must be merged into ${BASE}." | tee -a "$WLOG"

# Fresh status and task log files. Dry runs use their own suffix so they never
# truncate evidence from a real wave.
for NN in $TASKS; do
  NN2="$(printf '%02d' "$((10#$NN))")"
  rm -f "reports/codex-wave-${WAVE}-task-${NN2}${RUN_SUFFIX}.status" \
        "reports/codex-wave-${WAVE}-task-${NN2}${RUN_SUFFIX}.log" 2>/dev/null \
    || { echo "cannot clear prior task status/log for task $NN2" >&2; exit 2; }
done

# --- launch, throttled to JOBS concurrent ---
for NN in $TASKS; do
  run_one "$NN" &
  while [ "$(jobs -rp | wc -l)" -ge "$JOBS" ]; do sleep 2; done
done
wait || true

# --- aggregate ---
echo "" | tee -a "$WLOG"
echo "| Task | Effort | Result | Branch |" | tee -a "$WLOG"
echo "|------|--------|--------|--------|" | tee -a "$WLOG"
overall=0
expected="PASS"
[ "$DRY_RUN" = 1 ] && expected="DRY-RUN"
for NN in $TASKS; do
  NN2="$(printf '%02d' "$((10#$NN))")"
  res="$(cat "reports/codex-wave-${WAVE}-task-${NN2}${RUN_SUFFIX}.status" 2>/dev/null || echo UNKNOWN)"
  [ "$res" = "$expected" ] || overall=1
  echo "| ${NN2} | $(effort_for "$NN2") | ${res} | codex/task-${NN2} |" | tee -a "$WLOG"
done
echo "" | tee -a "$WLOG"
if [ "$overall" -eq 0 ] && [ "$DRY_RUN" != 1 ]; then
  echo "Review PASS branches, merge to ${BASE}, then run the next wave." | tee -a "$WLOG"
elif [ "$DRY_RUN" = 1 ]; then
  echo "Dry run complete; no branches or worktrees were created." | tee -a "$WLOG"
else
  echo "Wave failed. Review the per-task status and logs; do not start the next wave." | tee -a "$WLOG"
fi
echo "Per-task output: reports/codex-wave-${WAVE}-task-*.log · remove a worktree: git worktree remove ../tradebook-wt/task-NN" | tee -a "$WLOG"
exit "$overall"
