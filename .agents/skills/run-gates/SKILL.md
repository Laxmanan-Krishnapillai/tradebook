---
name: run-gates
description: Run tradebook's full verification gate suite plus the test-integrity check, and refuse to declare a task done until both pass. Use before finishing ANY implementation task.
---

# run-gates

"Done" means the gates are green — not that the code looks finished. Use this skill
to prove a task is actually complete before you stop.

## Steps

1. Make sure Docker is running (integration tests use Testcontainers / PostgreSQL 17).
2. Run the full gate suite:

       bin/verify.sh

   It runs, in order: `-warnaserror` build, unit tests, integration tests,
   architecture tests, Stryker mutation (break = 80), contract generation + drift
   check, and frontend lint + build + tests. It exits non-zero on the first failure.
3. Run the test-integrity tripwire against the branch point:

       bin/check-test-integrity.sh

4. If anything fails, STOP and fix the *code*. Never delete, `[Skip]`, comment out,
   or weaken a test to force a green gate. If you legitimately changed the behaviour a
   test asserts, that is allowed — record it with a commit trailer:

       Test-Change: <what behaviour changed and why the test had to change>

5. Only when both pass, reconcile acceptance criteria: output a table mapping each
   acceptance-criterion ID in the task spec to the test that proves it and its
   PASS/FAIL. Do not end with only a plan; the deliverable is working code.

## Rules

- Never hand-edit generated contracts (`src/Frontend/src/api/generated/`). Change the
  C# DTOs and regenerate.
- Make the smallest change that satisfies the spec. Do not add a library, abstraction,
  config option, or layer unless a named acceptance criterion requires it.
- Commit with: `bin/agent-commit.sh <type> <scope> <summary>` (conventional commits;
  commitlint enforces valid scopes).
