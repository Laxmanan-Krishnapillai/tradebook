# Claude CLI handoff — finish the Codex setup

The Codex setup was committed to `main` from a remote session that could not (a) write
under `.github/workflows/`, (b) reach the network to apply GitHub branch protection, or
(c) delete files. Everything below finishes those steps. Paste the prompt in the next
section into Claude Code CLI running at the repo root with full permissions.

---

## Prompt to give Claude CLI

You are working in the `tradebook` repo (remote `Laxmanan-Krishnapillai/tradebook`) with
full permissions. A prior session committed the Codex setup to `main` (commit message
"chore(repo): add codex setup — verify gate, overnight wave driver, test-integrity
guard"). Finish the parts that couldn't be done remotely. Do NOT touch my other
uncommitted work; only add the files named below. Read `AGENTS.md` first and follow it.
Use `bin/agent-commit.sh <type> <scope> <summary>` for commits (commitlint scopes
include `ci`, `repo`, `docs`). Do the following, then report what passed:

1. **CI workflow.** Copy `docs/codex/test-integrity.workflow.yml` to
   `.github/workflows/test-integrity.yml` verbatim.

2. **CODEOWNERS.** Create `.github/CODEOWNERS` with the exact content from the
   "CODEOWNERS" section of `docs/codex/branch-protection.md` (owner handle
   `@Laxmanan-Krishnapillai`).

3. **Required-check aggregation (recommended).** Add the `ci-required` job from the
   "Required status checks → Option B" section of `docs/codex/branch-protection.md` to
   `.github/workflows/ci.yml`, wired to `needs: [governance, contracts, backend,
   frontend, infra]` with `if: always()`. This prevents required checks from
   deadlocking on skipped conditional jobs.

4. **Commit** the three changes above with a `ci` scope, e.g.
   `bin/agent-commit.sh ci ci "add test-integrity workflow, CODEOWNERS, ci-required gate"`.

5. **Apply branch protection.** Confirm `gh auth status`, then run the `gh api -X PUT
   …/branches/main/protection` command from the "Apply protection" section of
   `docs/codex/branch-protection.md`. First open a scratch PR and run `gh pr checks` to
   read the EXACT check-run names, and set the `contexts` array to those exact strings
   (expected: `ci-required` and `test-integrity`). Delete the scratch PR/branch after.

6. **Repo hygiene** (the remote bridge couldn't delete files):
   - `git gc --prune=now` to clear the loose `.git/objects/**/tmp_obj_*` cruft.
   - Remove the leftover lock renames: `rm -f .git/*.lock.stale.* .git/*.lock.cleared* .git/HEAD.lock.* .git/index.lock.* 2>/dev/null` (only the `.stale`/`.cleared` ones — do not delete a live `.lock`).
   - Remove the superseded task copies: `git rm -r docs/tasks/_superseded_renumber` (or `rm -rf` if untracked), then commit with a `repo` scope.

7. **Codex config (optional).** If `~/.codex/config.toml` doesn't already set a model,
   merge `docs/codex/codex-config.toml.example` into it (do not clobber existing keys).

8. **Verify the setup end to end:**
   - With Docker running, run `bin/verify.sh` on a clean tree and report the result.
   - Run `DRY_RUN=1 bin/codex-overnight.sh 13 11` and confirm the printed `codex exec`
     invocation uses flags your installed Codex CLI accepts (`codex exec --help`). If any
     flag is rejected, fix `CODEX_EXEC_FLAGS` near the top of `bin/codex-overnight.sh`
     and commit with a `repo` scope.

9. **Push** the new commits to `origin/main`.

Report: which required-check contexts you set, the result of `bin/verify.sh`, and any
`codex exec` flag adjustments you made.

---

## Notes for you (not part of the prompt)

- If you're a solo owner, CODEOWNERS + required review means *agent-authored* PRs
  touching tests/gates need your approval; you can still admin-merge your own PRs
  (`enforce_admins` is `false`).
- Branch protection only affects PRs into `main` on GitHub — the local overnight driver
  is unaffected because it never pushes.
- The overnight task work goes onto `codex/<date>/integration`, not `main`; review and
  merge that in the morning.
