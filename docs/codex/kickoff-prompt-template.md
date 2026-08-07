You are implementing tradebook Task {{TASK_NUMBER}}. The authoritative, binding
specification is the committed file: {{SPEC_PATH}}. Read it in full first, together
with docs/architecture/decision-log.md and the root and nearest AGENTS.md.

GOAL
- Deliver working, merge-quality code that satisfies EVERY acceptance criterion in
  {{SPEC_PATH}}. The deliverable is code, not a plan.

CONTEXT (binding repo rules — see AGENTS.md)
- JWT auth on every endpoint except /health/live, /health/ready, and POST
  /api/v1/auth/login. Derive the actor from the JWT `sub` claim only.
- Bind SQL values as parameters and whitelist every dynamic identifier.
- Never hand-edit src/Frontend/src/api/generated/. Change C# DTOs and regenerate.
- Integration tests use PostgreSQL 17 via Testcontainers with Respawn; derive from
  DatabaseTestBase / PostgresDatabaseTestBase. Do not depend on a host database.
- Match existing patterns, libraries, and versions already in the repo.

CONSTRAINTS (anti-slop)
- Make the SMALLEST change that satisfies the spec. Do NOT add a library,
  abstraction, interface, config option, or layer unless a specific acceptance
  criterion in {{SPEC_PATH}} requires it — and if it does, name that criterion in
  the code/PR. Prefer the platform/stdlib and already-installed dependencies. No
  speculative or "adjacent" features. Keep the diff scoped and reviewable.

TEST POLICY
- You MAY change tests when the behaviour they assert legitimately changes under
  this task — that is expected. When you do, add a commit trailer:
      Test-Change: <one line: what behaviour changed and why the test changed>
- You MUST NOT delete, [Skip], comment out, or weaken a test just to get a green
  gate. Mutation testing (Stryker, break=80) catches weakened tests regardless.

DEFINITION OF DONE (do not declare done until all of these hold)
- `bin/verify.sh` exits 0 (build -warnaserror, unit + integration + architecture
  tests, Stryker mutation, contract-drift, frontend lint/build/test).
- Every acceptance-criterion ID in {{SPEC_PATH}} maps to a passing test.
- Commit with `bin/agent-commit.sh <type> <scope> <summary>` (conventional commits;
  valid scopes are enforced by commitlint).
- END your run with a table: acceptance-criterion ID → test name → PASS/FAIL. If
  anything is blocked, say so explicitly. Never fake, hardcode, or special-case a pass.

WORKFLOW
1. Plan first: restate scope and map each acceptance criterion to the test/command
   that will prove it.
2. Implement against the spec.
3. Run `bin/verify.sh` after changes; stop-and-fix until green — do not push forward
   on a red gate.
4. Reconcile the acceptance-criterion → test table. Do not end with only a plan.
