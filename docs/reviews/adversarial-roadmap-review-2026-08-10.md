# Adversarial Roadmap and Final-Design Review

**Review date:** 2026-08-10
**Reviewed commit:** `5fb211b` (`main`, matching `origin/main` at review time)
**Reviewer:** Codex
**Status:** Findings report; no remediation is included in this change

## 1. Scope

This review compares the implemented roadmap against:

- [`docs/tasks/README.md`](../tasks/README.md) and the individual task specifications;
- [`docs/codex/WAVES.md`](../codex/WAVES.md), including dependency order;
- [`docs/architecture/decision-log.md`](../architecture/decision-log.md);
- [`docs/architecture/entity-model.md`](../architecture/entity-model.md); and
- [`docs/design/DESIGN.md`](../design/DESIGN.md).

Task 17 is intentionally not assessed as complete because it was still in progress at
the reviewed commit. Where an already-merged task depends on Task 17, this report calls
out the resulting verification gap without grading the unfinished Task 17 work.

Severity meanings:

- **P1:** acceptance blocker or a gate that gives materially false confidence;
- **P2:** material implementation, design, or governance gap; and
- **P3:** documentation or process drift that is unlikely to break runtime behaviour by
  itself.

## 2. Executive summary

The roadmap should not yet be considered complete. The highest-risk pattern is that
several gates validate a narrow fixture or proxy instead of the production path they
claim to protect:

1. the contract gate compares DTO property names but never compares runtime OpenAPI;
2. the sqlc gate compiles one test-only probe while production repositories remain
   handwritten Dapper;
3. the UI import guardrail protects a synthetic directory but not the real feature
   directories; and
4. the Entra migration left the real-stack E2E workflow and visual tests on the retired
   password-login flow.

The findings affect Tasks 12, 15, 16, 19, 20, 21, and 24, plus the authoritative
identity and roadmap documentation. A full gate run performed while saving this report
also confirmed that the reviewed commit is red: build and 749 unit tests passed, but 5
of 59 integration tests failed.

## 3. Findings

### 3.1 [P1] The API contract-drift gate does not compare the runtime API

Task 16 requires TypeSpec to govern every endpoint and DTO and requires a separate
comparison with FastEndpoints' emitted OpenAPI. See
[`task-16-contract-first-api-typespec-openapi.md`](../tasks/task-16-contract-first-api-typespec-openapi.md)
and acceptance criterion `CONTRACT-03`.

The implemented [`scripts/check-contract-drift.sh`](../../scripts/check-contract-drift.sh)
only:

1. compiles TypeSpec;
2. runs [`scripts/compare-contract-dtos.py`](../../scripts/compare-contract-dtos.py);
3. regenerates the frontend client; and
4. compares generated-file hashes.

It never starts the API, retrieves runtime OpenAPI, or compares routes, HTTP verbs,
authentication, status codes, field types, optionality, or response shapes. The Python
comparator uses regular expressions to compare only DTO property-name sets.

This has already allowed concrete drift:

- [`docs/api/typespec/main.tsp`](../api/typespec/main.tsp) still declares
  `POST /api/v1/auth/login`;
- [`src/Frontend/src/api/generated/sdk.gen.ts`](../../src/Frontend/src/api/generated/sdk.gen.ts)
  still generates `authLogin` for that route;
- Task 12 explicitly requires the production login endpoint and generated login
  contracts to be retired; and
- the runtime has no `LoginEndpoint.cs`; only `LoginMapper.cs` remains.

The comparator was executed during this review and returned:

```text
C# and TypeSpec DTO fields agree for 62 models.
```

It therefore passes despite a confirmed route-level divergence.

**Required correction:** compare a normalized TypeSpec OpenAPI document with the
FastEndpoints-emitted runtime document, and remove the obsolete login operation and
generated contracts.

### 3.2 [P1] The Entra migration broke real-stack E2E and visual tests

[`src/Frontend/src/components/auth/LoginForm.tsx`](../../src/Frontend/src/components/auth/LoginForm.tsx)
now offers only Microsoft sign-in, but the E2E system still assumes local credentials:

- [`tests/e2e/src/support/environment.ts`](../../tests/e2e/src/support/environment.ts)
  fills `Username` and `Password` and posts to `/api/v1/auth/login`;
- [`tests/e2e/src/pages/BasePage.ts`](../../tests/e2e/src/pages/BasePage.ts) performs the
  same retired interaction;
- [`.github/workflows/ci-e2e-performance.yml`](../../.github/workflows/ci-e2e-performance.yml)
  configures the old local JWT signing key and obtains the k6 token from the deleted
  login route; and
- [`tests/e2e/src/specs/visual.spec.ts`](../../tests/e2e/src/specs/visual.spec.ts) and
  [`tests/e2e/src/specs/a11y.spec.ts`](../../tests/e2e/src/specs/a11y.spec.ts) expect a
  heading named `Sign in`, while the rendered heading is `Tradebook`.

The visual workflow now boots a real stack, but its test assumptions are still stale.
The E2E workflow cannot validate the Entra-based application in its current form.

**Required correction:** provide the non-production Entra automation flow required by
Task 12, migrate browser and k6 authentication to it, and update the visual/a11y
assertions to the current login screen.

### 3.3 [P1] Task 20's sqlc adoption is a test-only probe, not repo-wide data access

Task 20 states that all application data access flows through sqlc output and permits
handwritten Dapper only for the exhaustive exceptions in its section 4.1.

Current [`sqlc.yaml`](../../sqlc.yaml) includes only migrations 001-003, excluding
migrations 004-013. There is one query,
`GetContractPersistenceProbe`, and its generated `ContractsSql` class is consumed only
by [`GeneratedDataAccessIntegrationTests.cs`](../../tests/Tradebook.IntegrationTests/GeneratedDataAccessIntegrationTests.cs).
No production code imports or constructs `ContractsSql`.

Production repositories, including
[`DeliveryRepository.cs`](../../src/Backend/src/Tradebook.Infrastructure/Data/DeliveryRepository.cs)
and
[`ContractRepository.cs`](../../src/Backend/src/Tradebook.Infrastructure/Data/ContractRepository.cs),
continue to contain extensive handwritten static SQL and Dapper calls. Normal CRUD
repositories are not one of the documented exceptions.

Consequently, the sqlc no-drift gate proves only that the probe regenerates cleanly; it
does not provide compile-time validation for production queries. `DATA-07` is not
satisfied.

**Required correction:** point sqlc at the complete migration schema, migrate static
application queries to generated methods, and explicitly inventory only the allowed
exception queries.

### 3.4 [P1] Task 19's field-error and controlled-form requirements are not implemented

[`src/Frontend/src/components/ui/validated-form.tsx`](../../src/Frontend/src/components/ui/validated-form.tsx)
installs a resolver, but feature controls remain external `useState` values. For
example, [`ContractsPage.tsx`](../../src/Frontend/src/components/contracts/ContractsPage.tsx)
maintains `createRequest` independently and renders controlled inputs without React
Hook Form `register` or `Controller` bindings.

Additional gaps:

- feature forms use coarse handwritten `z.custom` checks, including the generic schema
  in [`DomainEntityPages.tsx`](../../src/Frontend/src/components/domain/DomainEntityPages.tsx),
  rather than reusing the generated TypeSpec Zod schemas where shapes match;
- [`applyProblemDetails`](../../src/Frontend/src/lib/validation/problem-details.ts)
  exists but has no production caller; and
- 409/422 Problem Details errors therefore are not mapped to fields with `setError` and
  cleared on resubmission.

`FORM-02` and `FORM-03` are unmet. `FORM-01` is only superficially covered by passing
external state through a resolver at submission time.

**Required correction:** make React Hook Form own form state, bind controlled primitives
through `Controller`, compose generated schemas, and call `applyProblemDetails` from
mutation error paths.

### 3.5 [P1] Task 24's UI import guardrail misses the actual feature directories

[`src/Frontend/eslint.config.js`](../../src/Frontend/eslint.config.js) classifies real
feature directories as `feature-auth`, `feature-contracts`, `feature-dashboard`, and so
on. The `boundaries/external` rule that prohibits raw Base UI imports applies only to
the generic `feature` type.

[`src/Frontend/tooling/ui-guardrails.mjs`](../../src/Frontend/tooling/ui-guardrails.mjs)
creates its negative fixture under `src/features/`, the only path classified as generic
`feature`. Thus `AGUI-02` proves that the synthetic fixture is blocked while leaving
the real feature directories outside the prohibition.

The remaining hard-gate claims are also incomplete:

- Argos runs only when `ARGOS_ENABLED == 'true'`;
- the workflow job named `e2e-a11y` runs only `visual.spec.ts`, not `a11y.spec.ts`; and
- [`docs/codex/branch-protection.md`](../codex/branch-protection.md) requires only
  `ci-required` and `test-integrity`, not the Argos or visual-QA checks.

**Required correction:** apply the external-import prohibition to all real feature
types, seed fixtures in representative real feature paths, run both visual and a11y
specifications, and make the promised checks required.

### 3.6 [P2] The implemented design system contradicts the final design document

[`docs/design/DESIGN.md`](../design/DESIGN.md) requires the semantic tokens
`--color-brand-600`, `--color-brand-900`, `--color-profit`, and `--color-loss`.
[`src/Frontend/src/styles.css`](../../src/Frontend/src/styles.css) instead exposes
`accent`, `buy`, and `sell` tokens.

Hardcoded palettes remain in runtime code:

- [`DashboardGrid.tsx`](../../src/Frontend/src/components/dashboard/DashboardGrid.tsx);
- [`ChartHost.tsx`](../../src/Frontend/src/components/visualizations/ChartHost.tsx); and
- [`lightweightChartsAdapter.ts`](../../src/Frontend/src/lib/charts/lightweightChartsAdapter.ts).

DESIGN also requires product screens to compose the private-registry `DataGrid`,
`Combobox`, and `Toolbar`. Product screens instead use `VirtualizedDataTable`, native
inputs/buttons, and handwritten toolbar markup. The registry components are effectively
disconnected demonstrations.

**Required correction:** reconcile token names in one authoritative source, move chart
palettes onto semantic tokens, and either adopt the registry components in product code
or revise the final design contract explicitly.

### 3.7 [P2] Task 15 does not cover every authoritative entity ID

Acceptance criterion `TYPE-02` requires a distinct Vogen type for every entity ID.
The authoritative entity model gives both `certificate_contracts` and `external_cogs`
their own UUID primary keys, but there is no `CertificateContractId` or
`ExternalCogsId` value object.

[`DomainSurfaceArchRuleTests.cs`](../../tests/Tradebook.ArchitectureTests/DomainSurfaceArchRuleTests.cs)
checks raw primitives only on already-existing exported properties. Missing domain
types therefore pass vacuously. The architecture suite passed all 185 tests during this
review without detecting the omissions.

**Required correction:** reconcile the complete authoritative entity list with the ID
catalog and add an architecture assertion that checks required ID-type presence, not
only primitive leakage from types that happen to exist.

### 3.8 [P2] Task 21 cannot yet satisfy its Task 17-dependent acceptance criteria

This is not a finding against unfinished Task 17 work. It is a finding that Task 21 was
accepted before its declared prerequisite existed.

[`src/Backend/src/Tradebook.Workers/Program.cs`](../../src/Backend/src/Tradebook.Workers/Program.cs)
is an empty generic host with no Wolverine configuration or handlers. The Aspire smoke
test in
[`AppHostSmokeTests.cs`](../../tests/Tradebook.IntegrationTests/AppHostSmokeTests.cs)
waits only for the API and calls `/health/ready`. It does not prove that workers and the
frontend are healthy, nor that a trace spans HTTP request -> Wolverine handler ->
SignalR push as `ASPIRE-01` and `ASPIRE-02` require.

**Required correction:** reopen Task 21 after Task 17 lands and verify all resources,
worker processing, and cross-component trace correlation.

### 3.9 [P2] The authoritative actor-identity rules contradict each other

Root [`AGENTS.md`](../../AGENTS.md) and the
[`entity-model.md`](../architecture/entity-model.md) dashboard definition require actor
identity to come from JWT `sub`. Current
[`ActorId.cs`](../../src/Backend/src/Tradebook.Api/Security/ActorId.cs) derives it from
the validated Entra `oid`, as Task 12 requires.

Task 12 describes this as implementing decision D15, but D15 in
[`decision-log.md`](../architecture/decision-log.md) is the .NET 10 and Central Package
Management decision. No authoritative identity decision clearly supersedes the binding
`sub` rule.

**Required correction:** record a dedicated identity decision, update AGENTS and the
entity model consistently, and document any actor-ID data migration implication.

### 3.10 [P3] Roadmap and verification documentation are stale

Tasks 11-24 remain marked `Specified` in [`docs/tasks/README.md`](../tasks/README.md)
despite most corresponding commits being merged. Task 10 was merged before several
later tasks even though [`WAVES.md`](../codex/WAVES.md) requires it to run last, and the
Task 10 specification still describes TypeGen as the contract gate.

Task 21 also documents VSTest-style `dotnet test --filter` commands after Task 22
migrated the suite to Microsoft.Testing.Platform. During this review, MTP warned that
the VSTest filter was ignored and ran all 185 architecture tests. The documented
focused Aspire/Database commands therefore do not provide the claimed focus.

**Required correction:** update task status, rerun Task 10 only after all prerequisites,
replace TypeGen language with TypeSpec, and use native MTP filtering syntax.

### 3.11 [P1] The reviewed commit does not meet the repository definition of done

The full `bin/verify.sh` suite was run after this report was created. It passed the
formatting probe, banned-API negative probe, warnings-as-errors build, and all 749 unit
tests. The integration suite then failed 5 of 59 tests:

1. `AppHostSmokeTests.ApiIsHealthyWhenTheGraphBoots` could not find the Aspire DCP
   orchestration component;
2. `SemanticSchemaStartupIntegrationTests.HostStartupFailsWhenADeclaredSemanticColumnIsMissing`
   expected `SemanticSchemaMismatchException`, but startup did not throw;
3. `OutboxDispatchTests.EveryMutationProducerEmitsTheRegisteredAggregateTypeToItsGroup`
   received HTTP 400;
4. `OutboxDispatchTests.PostingADeliveryPushesEntityChangedAndMarksTheOutboxRowProcessed`
   received HTTP 400; and
5. `AnalyticsQueryEndpointTests.SeededDeliveryReturnsColumnsAndRows` expected a JSON
   number but received a string.

The two outbox failures overlap the known in-progress Task 17 work and are recorded
without treating Task 17 as complete. The analytics failure is consistent with the
Task 16 money-as-string migration leaving a stale integration assertion. The semantic
schema and Aspire failures require separate diagnosis.

Because the suite stops on the first failing stage, architecture, mutation, contract,
and frontend gates were not reached in this full run. Regardless of attribution, the
reviewed commit does not currently satisfy the repository's exit-zero definition of
done.

## 4. Verification performed

The review performed the following focused checks:

| Check | Result | Interpretation |
|---|---|---|
| `python scripts/compare-contract-dtos.py` | PASS, 62 models | False positive: the removed runtime login route remains in TypeSpec/client |
| Architecture test project | PASS, 185 tests | Missing entity ID types are not detected by the current surface-only assertion |
| Documented VSTest-style focused filter | Ignored by MTP | The attempted focused run expanded to the full project |
| Aspire-focused integration attempt | Stopped during full-suite infrastructure setup | No full Aspire verdict; the documented filter did not isolate the test |
| Static production-use searches | Confirmed | No production `ContractsSql` usage and no production `applyProblemDetails` caller |
| Full `bin/verify.sh` | FAIL at integration: 5 failed, 54 passed | Build and 749 unit tests passed; later gates were not reached |
| `bin/check-test-integrity.sh` | PASS | No test files changed relative to reviewed commit `5fb211b` |

The initial review did not run the full suite because Task 17 was actively changing and
the checkout's JavaScript dependencies were partially installed. While saving this
report, the suite was rerun from Git Bash with the Windows SDK and Python paths made
explicit. The integration failure above is the resulting authoritative gate outcome.

## 5. Recommended remediation order

1. Implement a real runtime OpenAPI comparison and remove the obsolete login contract.
2. Repair Entra-compatible E2E/k6 authentication and make visual/a11y checks genuinely
   required.
3. Migrate production static SQL to sqlc or explicitly justify each allowed exception.
4. Complete RHF ownership, generated-schema reuse, and Problem Details field mapping.
5. Fix Task 24 guardrail coverage and reconcile DESIGN tokens/registry usage.
6. Add the missing authoritative entity ID types and presence-level architecture tests.
7. After Task 17 lands, reopen Task 21 and verify the complete Aspire graph and trace.
8. Resolve the `sub` versus `oid` authority conflict before further identity work.
9. Refresh roadmap statuses and rerun the final Task 10 verification last.

## 6. Completion position

Until the P1 findings are corrected and the relevant acceptance criteria are mapped to
tests that exercise production paths rather than probes, fixtures, or unused helpers,
the implemented roadmap should be treated as incomplete.
