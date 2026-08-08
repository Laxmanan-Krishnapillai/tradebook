#!/usr/bin/env bash
# bin/codex-overnight.sh — unattended, dependency-ordered wave runner for Codex.
#
# Runs tasks ONE AT A TIME in dependency order on an integration branch off main.
# For each task it: creates a task branch, runs `codex exec` against the committed
# spec, then gates with bin/verify.sh + bin/check-test-integrity.sh. On green it
# merges the task branch into the integration branch and continues; on red it
# STOPS (default), leaving the branch for review. Nothing is pushed and `main` is
# never modified — in the morning you review the integration branch and merge to
# main what passes review. (This preserves "a human owns the merge".)
#
# Usage:
#   bin/codex-overnight.sh                  # full linearized order (see DEFAULT_ORDER)
#   bin/codex-overnight.sh 13 11 14         # only these tasks, in this order
#   DRY_RUN=1 bin/codex-overnight.sh 13     # print the codex command, run nothing
#   KEEP_GOING=1 bin/codex-overnight.sh     # continue to next task even after a red gate
#
# Requires on PATH: codex, dotnet, npm, docker (running), git, timeout.
# NOTE: `codex exec` flag names vary by CLI version. Run `codex exec --help` and
# adjust CODEX_EXEC_FLAGS below if needed. ALWAYS do a `DRY_RUN=1` pass first.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"; cd "$ROOT" || exit 1

MODEL="${CODEX_MODEL:-gpt-5.6-sol}"
DATE="$(date +%Y%m%d)"
INTEG="codex/${DATE}/integration"
LOG="reports/codex-overnight-${DATE}.md"
TEMPLATE="docs/codex/kickoff-prompt-template.md"
DEFAULT_ORDER=(13 11 14 15 17 18 16 20 12 09 19 21 22 23 24 10)

# Decomposable, high-value tasks get Ultra (max reasoning + subagent delegation);
# the deep focused one gets xhigh; mechanical/QA get medium; everything else high.
effort_for()  { case "$1" in 14|16|17) echo ultra;; 12) echo xhigh;; 09|22|10) echo medium;; *) echo high;; esac; }
# Per-task wall-clock cap (seconds); Ultra/xhigh tasks fan out and take longer.
timeout_for() { case "$1" in 14|16|17|12) echo 10800;; 09|10|22) echo 5400;; *) echo 7200;; esac; }

DRY_RUN="${DRY_RUN:-0}"; KEEP_GOING="${KEEP_GOING:-0}"
ORDER=("$@"); [ "${#ORDER[@]}" -eq 0 ] && ORDER=("${DEFAULT_ORDER[@]}")

command -v git >/dev/null || { echo "git required" >&2; exit 2; }
mkdir -p reports
spec_for() { ls docs/tasks/task-"$1"-*.md 2>/dev/null | head -1; }
log() { printf '%s\n' "$*" | tee -a "$LOG"; }

if [ "$DRY_RUN" != 1 ]; then
  command -v codex  >/dev/null || { echo "codex CLI not found on PATH" >&2; exit 2; }
  command -v dotnet >/dev/null || { echo "dotnet not found on PATH" >&2; exit 2; }
  docker info >/dev/null 2>&1  || { echo "Docker must be running (integration tests need it)." >&2; exit 2; }
  if [ -n "$(git status --porcelain)" ]; then
    echo "Working tree is dirty — commit or stash before an overnight run." >&2; exit 2
  fi
fi

log "# Codex overnight run — ${DATE}"
log ""
log "Model: \`${MODEL}\` · integration branch: \`${INTEG}\` · order: ${ORDER[*]}"
log ""
log "| Task | Spec | Effort | Result | Branch |"
log "|------|------|--------|--------|--------|"

if [ "$DRY_RUN" != 1 ]; then
  git switch -c "$INTEG" main 2>/dev/null || git switch "$INTEG"
  dotnet tool restore >/dev/null 2>&1 || true
  dotnet restore src/Backend/Tradebook.sln >/dev/null 2>&1 || true
  ( cd src/Frontend && { [ -d node_modules ] || npm ci; } ) >/dev/null 2>&1 || true
fi

overall=0
for NN in "${ORDER[@]}"; do
  spec="$(spec_for "$NN")"
  if [ -z "$spec" ]; then log "| $NN | (spec not found) | - | SKIP | - |"; continue; fi
  eff="$(effort_for "$NN")"; tmo="$(timeout_for "$NN")"; branch="codex/${DATE}/task-${NN}"

  prompt="$(sed -e "s#{{TASK_NUMBER}}#${NN}#g" -e "s#{{SPEC_PATH}}#${spec}#g" "$TEMPLATE")"

  if [ "$DRY_RUN" = 1 ]; then
    echo "----- task ${NN}  (effort=${eff}, timeout=${tmo}s, spec=${spec}) -----"
    echo "codex exec --model ${MODEL} -c model_reasoning_effort=${eff} --sandbox workspace-write --ask-for-approval never \"<prompt rendered from ${TEMPLATE}>\""
    continue
  fi

  git switch "$INTEG" >/dev/null 2>&1
  git switch -c "$branch" >/dev/null 2>&1 || git switch "$branch"
  echo ">>> Task ${NN}  effort=${eff}  branch=${branch}"

  CODEX_EXEC_FLAGS=(--model "$MODEL" -c "model_reasoning_effort=${eff}" --sandbox workspace-write --ask-for-approval never)
  timeout "${tmo}" codex exec "${CODEX_EXEC_FLAGS[@]}" "$prompt"
  codex_rc=$?

  # Capture any agent edits that weren't self-committed, so the gate sees them.
  if [ -n "$(git status --porcelain)" ]; then
    git add -A
    git -c core.hooksPath=/dev/null commit -q --no-verify \
      -m "chore(repo): codex task ${NN} pre-gate snapshot" || true
  fi

  result="FAIL"
  if [ "$codex_rc" -ne 0 ]; then
    log "| $NN | $spec | $eff | FAIL (codex rc=${codex_rc}/timeout) | $branch |"
  elif bin/verify.sh && bin/check-test-integrity.sh "$INTEG"; then
    git switch "$INTEG" >/dev/null 2>&1
    if git merge --no-ff --no-edit "$branch" >/dev/null 2>&1; then
      result="PASS"; log "| $NN | $spec | $eff | PASS | ${branch} → ${INTEG} |"
    else
      log "| $NN | $spec | $eff | FAIL (merge conflict) | $branch |"
    fi
  else
    log "| $NN | $spec | $eff | FAIL (gate red) | $branch |"
  fi

  if [ "$result" != "PASS" ]; then
    overall=1
    git switch "$INTEG" >/dev/null 2>&1
    if [ "$KEEP_GOING" != 1 ]; then
      log ""; log "Stopped at task ${NN} (not green). Fix on \`${branch}\`, then re-run the remaining tasks."
      break
    fi
  fi
done

log ""
log "Done. Review \`${INTEG}\` (and any failed task branch) and merge to \`main\` what passes review."
exit "$overall"
