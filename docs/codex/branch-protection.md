# Branch protection & CODEOWNERS — tradebook

Repo: `Laxmanan-Krishnapillai/tradebook` · protected branch: `main`.

The point of this file is to make the deterministic gates a **hard, server-side
requirement** for every change that reaches `main`, and to make weakening the
verification stack require an explicit owner review. It complements the local gates
(`bin/verify.sh`) and the test tripwire (`bin/check-test-integrity.sh`).

Applying this needs GitHub admin rights and `gh auth` — see `docs/codex/claude-cli-handoff.md`.

## 1. How "per-wave" protection works

Each wave (see `docs/codex/WAVES.md`) lands as one or more PRs into `main`. Branch
protection applies the **same required gates to every PR**, so no wave can merge
without passing them — that is the per-wave enforcement. The overnight driver merges
task branches into a local `codex/<date>/integration` branch and never pushes, so it
is unaffected; protection only bites when you open the review PR into `main`.

## 2. CODEOWNERS

Create `.github/CODEOWNERS` with the content below. With "Require review from Code
Owners" enabled (§4), any PR that touches the gate stack or tests needs the owner's
explicit approval — a structural guard against an agent quietly weakening verification.

```
# tradebook code owners — protect the verification stack and tests.
# Requires branch protection with "require_code_owner_reviews": true.

/tests/                              @Laxmanan-Krishnapillai
**/*Tests*/                          @Laxmanan-Krishnapillai
/src/Frontend/**/*.test.ts           @Laxmanan-Krishnapillai
/src/Frontend/**/*.test.tsx          @Laxmanan-Krishnapillai
/src/Frontend/**/*.spec.ts           @Laxmanan-Krishnapillai
/src/Frontend/**/*.spec.tsx          @Laxmanan-Krishnapillai
/stryker-config.json                 @Laxmanan-Krishnapillai
/bin/verify.sh                       @Laxmanan-Krishnapillai
/bin/check-test-integrity.sh         @Laxmanan-Krishnapillai
/.github/                            @Laxmanan-Krishnapillai
/AGENTS.md                           @Laxmanan-Krishnapillai
/src/Backend/AGENTS.md               @Laxmanan-Krishnapillai
```

> Solo-dev note: GitHub won't let you approve your own PR, so with a single owner the
> practical effect is that **agent-authored** PRs touching these paths can't merge
> until you review them (you can still admin-merge your own hand-made PRs). That's the
> behaviour you want here.

## 3. Required status checks — avoid the skipped-job deadlock

`ci.yml` gates `backend` / `frontend` / `infra` behind a `paths-filter`, so they are
**skipped** on unrelated PRs. A required check that never runs blocks the PR forever,
so do **not** require those job names directly. Two options:

**Option A (simple):** require only the always-run jobs: `governance`, `contracts`,
and `test-integrity`. The conditional gates still run and show on the PR; CODEOWNERS +
review cover the rest.

**Option B (robust, recommended):** add one aggregation job to `.github/workflows/ci.yml`
that succeeds when every needed job passed *or was skipped*, and require just that
(`ci-required`) plus `test-integrity`:

```yaml
  ci-required:
    needs: [governance, contracts, backend, frontend, infra]
    if: always()
    runs-on: ubuntu-latest
    steps:
      - name: Require all gates to pass (skipped is OK, failure/cancelled is not)
        shell: bash
        run: |
          results='${{ join(needs.*.result, ',') }}'
          echo "gate results: $results"
          case "$results" in
            *failure*|*cancelled*) echo "a required gate did not pass"; exit 1 ;;
            *) echo "all required gates passed or were skipped" ;;
          esac
```

## 4. Apply protection (classic branch-protection API)

```bash
gh api -X PUT repos/Laxmanan-Krishnapillai/tradebook/branches/main/protection \
  -H "Accept: application/vnd.github+json" --input - <<'JSON'
{
  "required_status_checks": {
    "strict": true,
    "contexts": ["ci-required", "test-integrity"]
  },
  "enforce_admins": false,
  "required_pull_request_reviews": {
    "required_approving_review_count": 1,
    "require_code_owner_reviews": true,
    "dismiss_stale_reviews": true
  },
  "restrictions": null,
  "required_linear_history": true,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "required_conversation_resolution": true
}
JSON
```

If you chose Option A, set `"contexts": ["governance", "contracts", "test-integrity"]`.

## 5. Caveats

- **Exact check names.** Required-check `contexts` must match the check-run names
  GitHub reports. Open one throwaway PR, run `gh pr checks <n>` (or look at the PR
  Checks tab), and set `contexts` to those exact strings.
- **`enforce_admins: false`** lets you admin-override in a pinch (sensible for a solo
  repo); set `true` if you want the rules to bind you too.
- **`strict: true`** requires branches to be up to date with `main` before merging —
  good hygiene, slightly more rebasing.
- **Rulesets alternative.** The newer repository **Rulesets** API
  (`repos/{owner}/{repo}/rulesets`) can express the same policy with better UI; the
  classic API above is simpler and sufficient here.
