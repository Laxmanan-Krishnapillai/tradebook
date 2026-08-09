# Tradebook Azure Tier-1 infrastructure

This configuration provisions the D9 production footprint only: one Azure Container
App serving the API and built SPA, PostgreSQL Flexible Server 17 on private networking,
an Azure Container Registry, Key Vault, Log Analytics, and versioned Blob backups. It
does not provision a broker, cache, WORM policy, or Tier-2/Tier-3 service.

## Prerequisites and state

Use Terraform 1.11 or later because database and JWT credentials are ephemeral values
and are passed to Azure through write-only provider attributes. The Terraform operator
needs permission to create role assignments and is granted `Key Vault Secrets Officer`
on the new vault so it can create the initial secrets.

Bootstrap the Azure Storage backend separately. Enable blob versioning and soft delete
on that account before initializing this configuration; backend coordinates belong in
an uncommitted `backend.prod.hcl`, never in this repository.

Before enabling `.github/workflows/deploy.yml`, configure the GitHub `production`
environment with at least one required reviewer, prevent self-review, and restrict
deployment branches to `main`. Record the settings page or approved control ticket in
the environment-scoped `PRODUCTION_APPROVAL_POLICY_EVIDENCE` variable. The deployment
job refuses to run without that evidence pointer, and the GitHub deployment record is
the per-release evidence of who approved the run. Configure workload-identity federation
for the GitHub environment; its Azure principal needs ACR push plus read/update/start/stop
permissions for the one Tradebook Container App and its three jobs. Do not create a
long-lived client secret for the workflow.

```powershell
terraform -chdir=infra/terraform init -backend-config=backend.prod.hcl
terraform -chdir=infra/terraform fmt -check -recursive
terraform -chdir=infra/terraform validate
terraform -chdir=infra/terraform plan -var-file=prod.tfvars -out=tradebook.tfplan
terraform -chdir=infra/terraform apply tradebook.tfplan
```

Required variables are `name_prefix`, `environment`, `location`, `api_image`, and
`database_ops_image`. Both images must use an ACR digest reference ending in
`@sha256:<64 lowercase hex characters>`. A typical non-secret variable file is:

```hcl
name_prefix       = "acmetrading"
environment       = "prod"
location          = "northeurope"
api_image          = "cracmetradingprod.azurecr.io/tradebook-api@sha256:..."
database_ops_image = "cracmetradingprod.azurecr.io/tradebook-database-ops@sha256:..."
alert_email        = "tradebook-operations@example.com"
```

For a new environment, the Terraform-managed registry does not exist yet, so it cannot
already contain the two images required by the first full apply. Bootstrap only the
registry, using syntactically valid placeholders that are never deployed by the targeted
apply, then build the real images and replace both values in `prod.tfvars` with their
resolved digests:

```powershell
$bootstrapDigest = 'sha256:' + ('0' * 64)
terraform -chdir=infra/terraform apply `
  -target=azurerm_container_registry.this `
  -var-file=prod.tfvars `
  -var="api_image=bootstrap.invalid/tradebook-api@$bootstrapDigest" `
  -var="database_ops_image=bootstrap.invalid/tradebook-database-ops@$bootstrapDigest"

az acr build --registry cracmetradingprod --target runtime --image tradebook-api:bootstrap .
az acr build --registry cracmetradingprod --target database-ops --image tradebook-database-ops:bootstrap .
az acr repository show --name cracmetradingprod --image tradebook-api:bootstrap --query digest --output tsv
az acr repository show --name cracmetradingprod --image tradebook-database-ops:bootstrap --query digest --output tsv
```

Targeted apply is only the one-time registry bootstrap. Review and execute a normal saved
plan immediately afterward; routine changes must never use `-target`.

Do not put a database password, JWT signing key, connection string, storage key, or
service-principal secret in `.tfvars`. Increment `secret_version` to deliberately rotate
the generated PostgreSQL and JWT credentials. Review the saved plan before apply: a
credential literal in plan output is a deployment blocker.

## Database jobs

The `database-ops` image target contains PostgreSQL 17 client tools, the ordered SQL
migrations, and a checksum ledger runner. Its three jobs use separate managed identities:

- `tradebook-migration` is manual and holds only ACR pull and Key Vault secret-read roles.
- `tradebook-backup` runs at 02:00 UTC, uploads a custom-format `pg_dump` and SHA-256
  manifest to `backups/tradebook/YYYY/MM/`, and holds Blob Data Contributor on that
  container.
- `tradebook-restore` is manual, reads blobs but cannot write them, verifies the manifest,
  restores into a fresh `tradebook_restore_*` database, reapplies/validates migrations,
  and drops the validation database after a successful or failed rehearsal by default.

The deployment workflow updates both immutable images, runs the migration job to a
successful terminal state, deploys the API image, and then checks `/health/ready`. It
stops a migration execution that exceeds the deployment timeout and restores every
previous image reference after a later failure. Database migrations remain forward-only:
each migration must be compatible with the immediately preceding API image because an
image rollback deliberately does not attempt destructive schema rollback.

To rehearse a specific production backup without retaining the validation database:

```powershell
az containerapp job start `
  --name tradebook-restore `
  --resource-group <resource-group> `
  --container-name restore `
  --env-vars BACKUP_BLOB=tradebook/2026/08/tradebook-2026-08-07T02-00-00Z.dump RESTORE_DATABASE=tradebook_restore_20260807 RESTORE_KEEP_DATABASE=false
```

Set `RESTORE_KEEP_DATABASE=true` only for an explicitly approved recovery where the new
database must remain available. The runner always refuses an existing target database;
it never overwrites `tradebook`.

## Backup policy and observability

Blob versioning is enabled. Base blobs and versions under `backups/tradebook/` are kept
for 2,555 days, with a cool-tier transition after 30 days. Backups deliberately remain
in an online tier for their full retention window so the restore job can read them
directly. This is ordinary versioned retention per D6, not immutability or legal hold:
authorized storage operators can still delete data. Add WORM only after a written
compliance requirement.

Azure Monitor receives PostgreSQL, Key Vault, and Blob diagnostics and alerts on API
replica loss/5xx responses, database availability/storage/CPU, failed database jobs, and
a missing `backup.completed` log during a 24-hour window. Production plans require a
valid `alert_email`; replace or supplement that receiver with the organization's incident
routing integration after it is represented as reviewed infrastructure as code.

The nightly schedule gives a target RPO of 24 hours. No RTO is claimed until Task 10's
backup-restore rehearsal has recorded a measured baseline using representative data.
For local/CI rehearsal only, the scripts accept an absolute `BACKUP_OUTPUT_DIRECTORY`
or `BACKUP_INPUT_DIRECTORY`; production jobs leave these unset and therefore require
managed-identity Azure Blob access.

## Local PostgreSQL

The root Compose topology intentionally contains PostgreSQL 17 only:

```powershell
$env:POSTGRES_PASSWORD = '<local-only password>'
docker compose up -d --wait postgres
```

Migrations run on first volume initialization. Re-run the checksum-aware migration
runner against an existing volume with:

```powershell
docker compose exec -e PGHOST=/var/run/postgresql -e PGDATABASE=tradebook -e PGUSER=tradebook postgres bash /opt/tradebook/database-ops/run-migrations.sh
```

## Microsoft Entra tenant-admin checklist

Terraform manages the single-tenant Tradebook API/SPA applications and service principals, delegated `access_as_user` permission, exact user roles, exact redirect URIs, admin-consent grant, and assignment-required API enterprise application. Supply the public tenant UUID and three environment redirect URIs; outputs are non-secret IDs suitable for the API and frontend build. Do not create an SPA secret or request Microsoft Graph permissions.

A tenant application owner must review the plan, confirm owners, grant consent where CI lacks directory permission, assign users/groups only through the three app roles, and retain the plan plus assignment drift output as release evidence.
