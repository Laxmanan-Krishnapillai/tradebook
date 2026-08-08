#!/usr/bin/env bash
# bin/codex-task.sh — supervised, single-task DAYTIME runner for Codex.
#
# Interactive: you watch the plan and approve diffs. Creates a task branch (or a
# git worktree with --worktree so you can drive several in parallel), seeds an
# interactive Codex session with the task's kickoff prompt at the right effort,
# then gates with bin/verify.sh + bin/check-test-integrity.sh and prints the
# commit / PR commands. Nothing is auto-merged — you review and merge to main.
#
# Usage:
#   bin/codex-task.sh 15                 # branch codex/task-15 in the current checkout
#   bin/codex-task.sh --worktree 15      # isolated worktree ../tradebook-wt/task-15
#   bin/codex-task.sh --profile ultra 20 # force a specific profile (ultra/deep/quick/spark)
#
# Run several --worktree invocations in separate terminals/tmux panes to drive a
# whole wave in parallel (keep ~3-5 in flight — review is the bottleneck, not Codex).
# See docs/codex/WAVES.md for the wave order and docs/codex/DAYTIME.md for the flow.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"; cd "$ROOT" || exit 1

WORKTREE=0; PROFILE=""; NN=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    --worktree) WORKTREE=1 ;;
    --profile)  PROFILE="${2:-}"; shift ;;
    -h|--help)  sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*)         echo "unknown option: $1" >&2; exit 2 ;;
    *)          NN="$1" ;;
  esac; shift
done
[ -z "$NN" ] && { echo "usage: bin/codex-task.sh [--worktree] [--profile deep] <task-number>" >&2; exit 2; }
NN="$(printf '%02d' "$((10#$NN))")"   # 9 -> 09

spec="$(ls docs/tasks/task-"$NN"-*.md 2>/dev/null | head -1)"
[ -z "$spec" ] && { echo "no spec found for task $NN under docs/tasks/" >&2; exit 2; }

effort_for() { case "$1" in 14|16|17) echo ultra;; 12) echo xhigh;; 09|22|10) echo medium;; *) echo high;; esac; }
eff="$(effort_for "$NN")"
# Effort (incl. ultra for 14/16/17, xhigh for 12) is applied via -c below;
# --profile stays a manual override if you want to force one.

command -v codex >/dev/null || { echo "codex CLI not found on PATH" >&2; exit 2; }
command -v git   >/dev/null || { echo "git required" >&2; exit 2; }

BR="codex/task-${NN}"
if [ "$WORKTREE" = 1 ]; then
  WT="../tradebook-wt/task-${NN}"
  git worktree add -B "$BR" "$WT" main || { echo "worktree add failed" >&2; exit 2; }
  cd "$WT" || exit 2
  echo "worktree: $WT  (branch $BR off main)"
else
  git switch -c "$BR" main 2>/dev/null || git switch "$BR" || exit 2
  echo "branch: $BR"
fi

prompt="$(sed -e "s#{{TASK_NUMBER}}#${NN}#g" -e "s#{{SPEC_PATH}}#${spec}#g" docs/codex/kickoff-prompt-template.md)"

echo
echo "Task ${NN}  ·  spec ${spec}  ·  effort ${eff}${PROFILE:+  ·  profile ${PROFILE}}"
echo "Launching interactive Codex — review the plan, approve diffs, let it run bin/verify.sh."
echo "When you exit Codex this script re-runs the gates and prints the PR command."
echo
CODEX_FLAGS=(-c "model_reasoning_effort=${eff}")
[ -n "$PROFILE" ] && CODEX_FLAGS+=(--profile "$PROFILE")
# Interactive & supervised — approvals come from your ~/.codex config (on-request).
codex "${CODEX_FLAGS[@]}" "$prompt"

echo
echo "=== gating $(git rev-parse --abbrev-ref HEAD) ==="
if bin/verify.sh && bin/check-test-integrity.sh main; then
  echo
  echo "✔ gates green."
  if [ -n "$(git status --porcelain)" ]; then
    echo "  Uncommitted changes remain — commit with:"
    echo "     bin/agent-commit.sh <type> <scope> \"task ${NN}: <summary>\""
  fi
  echo "  Open a PR for review:"
  echo "     gh pr create --base main --head ${BR} --fill"
else
  echo
  echo "✘ gates red — fix, then re-run: bin/verify.sh"
fi
