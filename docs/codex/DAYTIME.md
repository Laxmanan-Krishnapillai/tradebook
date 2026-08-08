# Daytime (supervised) Codex workflow

`bin/codex-overnight.sh` is for unattended runs (headless, branch-per-task,
gate-and-stop, review in the morning). During the day you're present, so run tasks
**supervised** and **merge as you go** — faster feedback and you own each merge in real
time. Use `bin/codex-task.sh`.

## What changes vs overnight

- **Supervised, not headless.** `approval_policy = on-request` (the default in the
  config). Codex plans first; you approve; you watch diffs. No `--ask-for-approval never`.
- **Merge cadence.** Review and merge each task's PR into `main` immediately — no nightly
  integration branch. Each new task branches off the freshly-updated `main`, so
  dependencies are satisfied as you go.
- **Parallelism is the speed lever.** Run 2–5 tasks *from the same wave* at once in
  separate git worktrees / terminals, capped by your review bandwidth.
- **Everything else is the same:** gates (`bin/verify.sh`), the `run-gates` skill, the
  per-task effort map, and the wave order (`docs/codex/WAVES.md`).

## One task at a time (simplest)

```bash
bin/codex-task.sh 15
```

Creates `codex/task-15` off `main`, opens an interactive Codex session seeded with the
task's kickoff prompt at the right effort (xhigh for 12 & 16, medium for 09/22/10, high
otherwise). Review the plan, approve, let it implement and run `bin/verify.sh`. When you
exit Codex the script re-gates and prints the commit + `gh pr create` commands. Review
the diff, merge to `main`, move to the next ready task.

## A whole wave in parallel (fastest)

Open one terminal (or tmux pane) per task in the wave, each in its own worktree:

```bash
bin/codex-task.sh --worktree 14
bin/codex-task.sh --worktree 15
bin/codex-task.sh --worktree 17
bin/codex-task.sh --worktree 18
```

Each gets an isolated checkout under `../tradebook-wt/task-NN` on its own branch, so the
agents never collide. Review and merge PRs as they turn green; when the wave is merged,
start the next wave. Keep 3–5 in flight — beyond that, review is the bottleneck, not
Codex. Remove a finished worktree with `git worktree remove ../tradebook-wt/task-NN`.

> Worktrees that run integration tests each need Docker; a single local Docker/Postgres
> is fine since Testcontainers gives every run its own container and Respawn resets state.

## Optional: hand the easy ones to the cloud

While you drive the hard tasks (12, 16, 17) locally, you can delegate independent,
well-specified tasks to Codex cloud in parallel and review those PRs as they land. The
cloud image has no .NET/Docker, so keep Testcontainers/Aspire/Playwright tasks (09, 21,
parts of 24) local — see `docs/codex/WAVES.md` and the playbook.

## Review depth

Deep-review the foundational, contract, and auth tasks (13, 16, 12, 24); a glance for the
mechanical ones. Confirm each task's acceptance-criterion → test table before merging.
Because you merge to `main` continuously, branch protection + the `test-integrity` gate
(`docs/codex/branch-protection.md`) matter more here than overnight — they're what stop a
supervised-but-rushed merge from slipping a weakened test through.
```

