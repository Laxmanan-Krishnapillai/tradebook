# Driving Codex on tradebook — runbook

This folder configures OpenAI Codex to implement the roadmap tasks (09–24) safely and
without slop. The principle: **the gates decide, the prompt steers.** The repo already
has strong deterministic gates (build `-warnaserror`, Stryker mutation 85/80/80,
architecture tests, Testcontainers integration, contract-drift, ESLint boundaries,
commitlint); this setup gives Codex a single entry point to those gates, the rules to
respect them, and an unattended runner that sequences the tasks in dependency order.

## What's here

| File | Purpose |
|---|---|
| `bin/verify.sh` | One command that runs every gate (== CI). The definition of done. |
| `bin/check-test-integrity.sh` | Tripwire: flags unjustified test weakening/deletion. |
| `.github/workflows/test-integrity.yml` | Same tripwire in CI on every PR/push. |
| `bin/codex-overnight.sh` | Unattended, dependency-ordered wave runner (branch-per-task, gated). |
| `docs/codex/WAVES.md` | The dependency-ordered plan + linearized order. |
| `docs/codex/kickoff-prompt-template.md` | The per-task prompt (four-element + acceptance-criteria contract). |
| `docs/codex/codex-config.toml.example` | Recommended `~/.codex/config.toml`. |

## One-time setup

1. **Install / update the Codex CLI** and sign in with your ChatGPT account (Pro x20):
   `codex login` (use `codex login status` to confirm). Keep the CLI current — older
   builds reject `gpt-5.6-sol`.
2. **Config:** copy `docs/codex/codex-config.toml.example` into `~/.codex/config.toml`
   (merge with anything you already have). It defaults to **Fast mode** (`service_tier =
   "fast"` — quality-neutral, ~1.5x speed at ~2.5x credits; turn off for overnight) and
   adds an `ultra` profile for the decomposable tasks (14, 16, 17).
3. **Toolchain on PATH:** `dotnet` (SDK per `global.json`; Task 13 moves this to .NET 10),
   `node`/`npm`, `git`, and **Docker running** (integration tests use Testcontainers).
   Run the `.sh` scripts from Git Bash or WSL.
4. **Sanity-check the gate** once on a clean tree: `bin/verify.sh` should pass.

## Everyday (interactive) use

- Implement one task locally, watching diffs:
  `codex --profile deep` for a Very-High task (12, 16), else the default profile.
  Point it at the spec, e.g. *"Implement Task 15 per docs/tasks/task-15-*.md.
  Definition of done: bin/verify.sh exits 0 and every acceptance criterion maps to a
  test. Follow AGENTS.md."*
- Quick mechanical edits / fast iteration: `codex --profile spark` (fast, but small
  context — single-file scope only).
- Before you consider anything done: `bin/verify.sh`.

## Overnight (unattended) use

The driver runs tasks **one at a time in dependency order** on a nightly integration
branch off `main`. Each task gets its own branch; it must pass `bin/verify.sh` and
`bin/check-test-integrity.sh`; on green it merges into the integration branch and the
next task starts; on a red gate it **stops** and leaves the branch for you. `main` is
never touched and nothing is pushed — you review and merge in the morning.

```bash
# 1) ALWAYS dry-run first — prints the codex command per task, runs nothing:
DRY_RUN=1 bin/codex-overnight.sh 13 11

# 2) Start small the first night (foundation only), review in the morning:
bin/codex-overnight.sh 13 11

# 3) Once you trust it, hand it a wave or the whole linearized order:
bin/codex-overnight.sh                 # 13 11 14 15 17 18 16 20 12 09 19 21 22 23 24 10
KEEP_GOING=1 bin/codex-overnight.sh    # don't stop on the first red gate
```

Requirements before a real run: clean working tree, Docker running, `codex`/`dotnet`/
`npm`/`timeout` on PATH. Effort and per-task wall-clock caps are set inside the script
(xhigh for 12 & 16; medium for 09/22/10; high otherwise). Output is logged to
`reports/codex-overnight-<date>.md`.

> `codex exec` flag names drift between CLI versions. If the dry-run shows flags your
> version doesn't accept, run `codex exec --help` and adjust `CODEX_EXEC_FLAGS` near
> the top of `bin/codex-overnight.sh`.

## The morning after

1. `git log --oneline main..codex/<date>/integration` — see what landed.
2. Review the integration branch as you would any PR — deep review the foundational,
   contract, and auth tasks (13, 16, 12, 24); a glance for mechanical ones. Confirm each
   task's acceptance-criterion → test table.
3. Merge the parts you accept into `main`; drop or re-run the rest.
4. If the run stopped on a red gate, the failing work is on its `codex/<date>/task-NN`
   branch — fix or re-prompt, then re-run the remaining tasks.

## Test policy (important)

Changing a test to match **intentionally changed behaviour is expected and allowed** —
the agent just has to say why, via a commit trailer `Test-Change: <reason>`. What's
forbidden is deleting/skipping/weakening a test to fake a green gate.
`bin/check-test-integrity.sh` flags suspicious reductions that lack a `Test-Change:`
rationale, and Stryker mutation testing independently fails if the suite actually got
weaker. Legitimate changes pass; cheating doesn't.

## Guardrails that make this safe

- Branch-per-task + integration branch → `main` stays clean; you own the merge.
- `--sandbox workspace-write` (never full access); per-task `timeout`; stop-on-red.
- Every gate that runs in CI also runs locally via `bin/verify.sh`, so "green
  overnight" means the same thing as "green in CI".
