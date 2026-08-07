# Tradebook Azure Tier-1 infrastructure

This directory provisions the production Tier-1 footprint: an Azure Container App,
PostgreSQL Flexible Server 17 on a private subnet, Key Vault, Log Analytics, and a
versioned Blob backup container. It deliberately creates no broker, cache, or
Tier-2/Tier-3 resources.

Initialize the remote state backend with environment-specific Azure Storage values
outside source control, then plan with a non-secret variable file:

```powershell
terraform -chdir=infra/terraform init -backend-config=backend.prod.hcl
terraform -chdir=infra/terraform plan -var-file=prod.tfvars
```

`name_prefix`, `environment`, `location`, and `api_image` are mandatory inputs. Azure
generates and stores the database password and JWT key in Key Vault; do not put them in
`.tfvars` files.

Backups are retained in the versioned `backups` Blob container for at least seven years.
A migration runner and the scheduled `pg_dump` job require a separate, reviewed runtime
contract; see `docs/architecture/spec-issues.md` before enabling production deployment.
