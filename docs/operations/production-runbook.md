# Tradebook production launch runbook

Use this runbook to plan, deploy, verify, or roll back the Azure Tier-1 Tradebook
platform. The intended readers are the release owner, database owner, and incident
commander for the production change.

## Current launch blockers

Do not deploy until all of these are resolved and reviewed:

- Task 09 must be merged, verified, and marked `Implemented` in the task index.
- The Azure backup module's immutable-container policy conflicts with decision D6. A
  non-WORM seven-year version-retention contract must replace it.
- Production database migration execution and the scheduled `pg_dump` identity/job have
  no reviewed runtime contract yet.
- Task 10 mentions Caddy, but the implemented D14 topology uses Azure Container Apps
  ingress directly and contains no Caddy deployment contract. This runbook follows the
  implemented Azure ingress topology and does not claim Caddy is present.

The detailed gaps and proposed resolutions are recorded in
[`spec-issues.md`](../architecture/spec-issues.md).

## Prerequisites

- A clean checkout of the exact release commit, with Tasks 01–10 marked `Implemented`.
- Independent approval of the immutable container image and Terraform plan.
- Azure CLI authentication to the intended tenant and subscription.
- Terraform backend and non-secret production variable files stored outside source
  control.
- PostgreSQL network access and credentials supplied through `DATABASE_URL`.
- A valid test identity JWT in `API_JWT` for authenticated k6 requests.
- Docker, .NET 9, Node.js, npm, PostgreSQL client tools, Terraform, k6, and Git Bash.

Never put passwords, JWT signing keys, access tokens, backend configuration, or production
variable files in this repository.

## 1. Preflight

From the repository root, run:

```bash
scripts/production-runbook.sh preflight
```

This requires a clean worktree, checks the active Azure identity, validates Terraform,
and confirms that local compose contains exactly `postgres` and `api` with PostgreSQL 17.
Stop if the displayed Azure tenant/subscription is not the approved production target.

## 2. Plan infrastructure

Use an immutable image reference such as a digest in the reviewed variable file. Then:

```bash
export TF_BACKEND_CONFIG=/secure/path/backend.prod.hcl
export TF_VARS_FILE=/secure/path/prod.tfvars
export TF_PLAN_FILE=/secure/path/tradebook-production.tfplan
scripts/production-runbook.sh plan
```

Have a second operator review the complete plan. Reject it if it creates a broker, cache,
TimescaleDB, a WORM/immutability policy, infrastructure beyond Azure Tier 1, or any public
database path.

## 3. Apply the reviewed plan

Only after approval:

```bash
export TF_PLAN_FILE=/secure/path/tradebook-production.tfplan
export TRADEBOOK_PRODUCTION_CONFIRM=APPLY_REVIEWED_PLAN
scripts/production-runbook.sh apply
```

The helper deliberately accepts only the saved plan. It does not create an implicit plan
at apply time.

## 4. Run database migrations

List the release's ordered migrations with:

```bash
scripts/production-runbook.sh show-migrations
```

Execute them only through the reviewed production migration job once its ownership,
identity, locking, retry, and migration-history contract is resolved. Do not loop over all
SQL files against an existing production database: the current scripts are bootstrap SQL,
not an idempotent migration runner.

## 5. Verify the deployed platform

Set the deployed API URL and database/JWT credentials, then run the full sentinel:

```bash
export API_BASE_URL=https://the-approved-container-app-host
export DATABASE_URL='postgresql://...'
export API_JWT='...'
scripts/production-runbook.sh verify
```

On Windows, the equivalent entry point is:

```powershell
./scripts/platform-verify.ps1
```

The liveness and readiness routes are intentionally anonymous platform probes. Readiness
executes a real PostgreSQL query; every business API and the SignalR hub still requires a
JWT. A non-200 readiness response is a deployment failure.

## 6. Rehearse the latest backup

Run immediately after the nightly custom-format dump while source writes are quiescent:

```bash
export DATABASE_URL='postgresql://...'
export BACKUP_PATH=/secure/path/latest.dump
scripts/production-runbook.sh rehearse-backup
```

The rehearsal restores into a fresh, randomly port-mapped `postgres:17` container and
compares every public table's row count with the source. Any restore error or count
mismatch fails. Retain its terminal output with the release evidence.

## Rollback

1. Stop rollout traffic changes and preserve logs, the Terraform plan, and verification
   output.
2. Revert `api_image` to the previously approved immutable image, create a new Terraform
   plan, obtain review, and apply that saved plan. Never retag an image in place.
3. Do not reverse database changes ad hoc. If schema/data rollback is necessary, restore
   the verified pre-deployment dump into a new PostgreSQL server, compare row counts, and
   switch only after database-owner approval.
4. Re-run full platform verification after rollback. Keep the failed environment available
   for investigation unless the incident commander authorizes disposal.

## Escalation

- Database migration, audit, or restore failure: database owner and incident commander.
- Authentication, 409 conflict, or SignalR failure: backend owner and security owner.
- Terraform, Key Vault, storage retention, or ingress failure: infrastructure owner.
- Playwright or k6 regression: Task 09 QA owner; do not edit the baseline to pass.

Record the failing command, exact exit code, release commit, image digest, Azure
subscription, UTC timestamp, and relevant logs in the incident record.
