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
mkdir -p reports
WLOG="reports/codex-wave-${WAVE}.md"

command -v git >/dev/null || { echo "git required" >&2; exit 2; }
if [ "$DRY_RUN" != 1 ]; then
  command -v codex  >/dev/null || { echo "codex not on PATH" >&2; exit 2; }
  command -v dotnet >/dev/null || { echo "dotnet not on PATH" >&2; exit 2; }
  docker info >/dev/null 2>&1  || { echo "Docker must be running (integration tests need it)." >&2; exit 2; }
fi

spec_for() { ls docs/tasks/task-"$1"-*.md 2>/dev/null | head -1; }
render()    { sed -e "s#{{TASK_NUMBER}}#$1#g" -e "s#{{SPEC_PATH}}#$2#g" docs/codex/kickoff-prompt-template.md; }

run_one() {
  local NN spec eff wt br log st
  NN="$(printf '%02d' "$((10#$1))")"
  spec="$(spec_for "$NN")"; eff="$(effort_for "$NN")"
  wt="../tradebook-wt/task-${NN}"; br="codex/task-${NN}"
  log="reports/codex-wave-${WAVE}-task-${NN}.log"; st="reports/codex-wave-${WAVE}-task-${NN}.status"
  if [ -z "$spec" ]; then echo "NO-SPEC" >"$st"; return; fi
  if [ "$DRY_RUN" = 1 ]; then
    echo "[dry] task $NN  effort=$eff  worktree=$wt  branch=$br off $BASE  spec=$spec"
    echo "DRY-RUN" >"$st"; return
  fi
  { [ -d "$wt" ] || git worktree add -B "$br" "$wt" "$BASE"; } >"$log" 2>&1 \
    || { echo "WORKTREE-FAIL" >"$st"; return; }
  ( cd "$wt" && codex exec --model "$MODEL" -c "model_reasoning_effort=${eff}" \
        --sandbox workspace-write --ask-for-approval never "$(render "$NN" "$spec")" ) >>"$log" 2>&1
  if ( cd "$wt" && bin/verify.sh && bin/check-test-integrity.sh "$BASE" ) >>"$log" 2>&1; then
    echo "PASS" >"$st"
  else
    echo "FAIL" >"$st"
  fi
}

echo "# Codex wave ${WAVE}: ${TASKS}" | tee "$WLOG"
echo "" | tee -a "$WLOG"
echo "Base: \`${BASE}\` · JOBS=${JOBS} · model ${MODEL}. Prior waves must be merged into ${BASE}." | tee -a "$WLOG"

# fresh status files
for NN in $TASKS; do rm -f "reports/codex-wave-${WAVE}-task-$(printf '%02d' "$((10#$NN))").status" 2>/dev/null || true; done

# --- launch, throttled to JOBS concurrent ---
for NN in $TASKS; do
  run_one "$NN" &
  while [ "$(jobs -rp | wc -l)" -ge "$JOBS" ]; do sleep 2; done
done
wait

# --- aggregate ---
echo "" | tee -a "$WLOG"
echo "| Task | Effort | Result | Branch |" | tee -a "$WLOG"
echo "|------|--------|--------|--------|" | tee -a "$WLOG"
for NN in $TASKS; do
  NN2="$(printf '%02d' "$((10#$NN))")"
  res="$(cat "reports/codex-wave-${WAVE}-task-${NN2}.status" 2>/dev/null || echo UNKNOWN)"
  echo "| ${NN2} | $(effort_for "$NN2") | ${res} | codex/task-${NN2} |" | tee -a "$WLOG"
done
echo "" | tee -a "$WLOG"
echo "Review PASS branches, merge to ${BASE}, then run the next wave." | tee -a "$WLOG"
echo "Per-task output: reports/codex-wave-${WAVE}-task-*.log · remove a worktree: git worktree remove ../tradebook-wt/task-NN" | tee -a "$WLOG"
