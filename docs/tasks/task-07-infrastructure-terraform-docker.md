# Task 07: Tier-1 Infrastructure as Code (Terraform, Azure) & Local Docker Compose

> **REWRITTEN 2026-08-06; IMPLEMENTATION-SYNCED 2026-08-07** per [`architecture/decision-log.md`](../architecture/decision-log.md) **D9** (Tier 1 only) and **D14** (Azure, not AWS — the organization's tenant and existing production PostgreSQL are Azure; the previous spec's AWS target was never grounded in org reality, and its Aurora+TimescaleDB combination was impossible anyway). Local Compose now contains PostgreSQL 17 only and applies the ordered migrations through the shared checksum-ledger contract. Tiers 2–3, NATS, TimescaleDB, Redis, ScyllaDB, and Salesforce integration are deleted. This file fully replaces the previous spec; filename kept for link stability.

- **Prerequisites**: Task 01 (migrations to run), Task 02 (API container)
- **Consumed by**: Task 09 (E2E env), Task 10 (deploy verification)
- **Complexity**: Medium

---

## 1. Scope

### In scope
1. The root `docker-compose.yml` for local dev + CI database operations: exactly one service, plain `postgres:17`, with deterministic migration initialization. The API and frontend run outside Compose during local development.
2. Terraform for one production environment on Azure: resource group, Container Apps, PostgreSQL Flexible Server 17, Blob storage (versioned backups per D6), Key Vault, Log Analytics.
3. GitHub Actions: CI (build/test, path-filtered) and CD (deploy image on `main`).
4. Separate migration, nightly backup, and manual restore Container Apps Jobs using the `database-ops` image target.

### Out of scope
- Multi-region, autoscaling beyond ACA defaults, WAF/CDN (add when a growth signal exists — D9).
- Any message broker or cache service (D2; HybridCache is in-process, Task 02).

---

## 2. Repository layout (monorepo, decision-log D12)

```
infra/
  terraform/
    README.md          # bootstrap, deployment, restore, and operating procedures
    main.tf            # providers, remote backend contract, resource group
    network.tf         # VNet, delegated subnets, and PostgreSQL private DNS
    database.tf        # PostgreSQL Flexible Server 17 + tradebook database
    registry.tf        # Azure Container Registry
    identities.tf      # per-workload managed identities and least-privilege roles
    containerapp.tf    # ACA environment + API app + ingress/probes
    jobs.tf            # migration, backup, and restore Container Apps Jobs
    storage.tf         # versioned backup storage + seven-year lifecycle retention
    keyvault.tf        # state-safe generated credentials and connection string
    monitoring.tf      # diagnostics, action group, and operational alerts
    variables.tf       # validated non-secret inputs and digest-pinned images
    outputs.tf         # API, database, registry, storage, and job outputs
  compose/postgres-init/
    00-run-migrations.sh
  database-ops/
    run-migrations.sh
    backup.sh
    restore.sh
  validation/
    verify-tier1.ps1
    verify-backup-restore.sh
src/Database/Migrations/ # ordered NNN_description.sql migration source
docker-compose.yml       # repo root; PostgreSQL 17 only
.github/workflows/ci.yml
.github/workflows/deploy.yml
Dockerfile               # runtime and database-ops image targets
```

---

## 3. Local development — `docker-compose.yml` (repo root)

```yaml
services:
  postgres:
    image: postgres:17
    environment:
      POSTGRES_DB: tradebook
      POSTGRES_USER: tradebook
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?set POSTGRES_PASSWORD in .env}
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./infra/compose/postgres-init:/docker-entrypoint-initdb.d:ro
      - ./infra/database-ops:/opt/tradebook/database-ops:ro
      - ./src/Database/Migrations:/opt/tradebook/migrations:ro
    healthcheck:
      test:
        - CMD-SHELL
        - >-
          test "$$(cat /proc/1/comm)" = postgres &&
          test "$$(psql --username tradebook --dbname tradebook --tuples-only --no-align
          --command 'SELECT count(*) FROM schema_migrations' 2>/dev/null)" -eq
          "$$(find /opt/tradebook/migrations -maxdepth 1 -type f -name '*.sql' | wc -l)"
      interval: 5s
      timeout: 3s
      retries: 10

volumes:
  pgdata:
```

`infra/compose/postgres-init/00-run-migrations.sh` exports the local Unix-socket connection values and invokes `infra/database-ops/run-migrations.sh`. The runner:

- accepts only the trusted absolute migration directory and filenames matching `NNN_description.sql`;
- sorts migrations lexically, takes the `tradebook-schema-migrations` PostgreSQL advisory lock, and records `version`, SHA-256 checksum, and application time in `schema_migrations`;
- refuses to continue if an already-applied migration's checksum changes; and
- applies each pending migration in its own transaction with `ON_ERROR_STOP` enabled.

Extensions are part of the ordered migration set (`001_extensions_and_enums.sql` creates `btree_gist`); there is no separate `01-extensions.sql`. A new Compose volume runs all migrations automatically. Against an existing volume, rerun the same idempotent/checksum-aware contract with:

```bash
docker compose exec \
  -e PGHOST=/var/run/postgresql \
  -e PGDATABASE=tradebook \
  -e PGUSER=tradebook \
  postgres bash /opt/tradebook/database-ops/run-migrations.sh
```

Rules:
- **No API, frontend, NATS, Redis, or any other service.** D9 requires `docker compose config --services` to return exactly `postgres`.
- There is exactly ONE compose file in the repo, at the root, owned by this task. Other tasks reference it; they never define their own.
- The API runs directly with `dotnet run --project src/Backend/src/Tradebook.Api/Tradebook.Api.csproj`; the frontend runs `npm run dev` from `src/Frontend` (Vite, port 5173, proxying `/api` and `/hubs` to 8080).

## 4. Production image — `Dockerfile` (repo root)

The Dockerfile defines four named stages and two deployable targets: (1) `node:22-bookworm-slim` builds `src/Frontend` to `dist/`; (2) the .NET 9 SDK publishes `Tradebook.Api` Release using standard JIT (no `PublishAot`, D7); (3) `database-ops`, based on `postgres:17-bookworm`, installs checksum-pinned AzCopy and packages the migrations plus migration/backup/restore scripts; and (4) the .NET 9 ASP.NET `runtime` target copies the API and SPA, runs as the non-root `tradebook` user, exposes port 8080, and defines an executable `/health/live` health check. The API serves the SPA via static files + fallback routing; ACA ingress terminates TLS, so no Caddy container is needed in production.

## 5. Terraform (azurerm ≥ 4.x)

Normative resource set — implementer fills standard boilerplate, but these choices are fixed:

| Concern | Resource | Fixed decisions |
| :--- | :--- | :--- |
| Database | `azurerm_postgresql_flexible_server` | PG **17**, `B_Standard_B2s` to start; `pg_stat_statements` only — **no TimescaleDB** (D3); delegated private subnet + private DNS; database name `tradebook` |
| Compute | `azurerm_container_app` + `azurerm_container_app_environment` | Single app `tradebook-api`, min 1 / max 2 replicas, external ingress on 8080, startup/liveness/readiness probes, API image pinned by digest |
| Registry | `azurerm_container_registry` | Basic ACR, admin credentials disabled; API and database-ops workloads pull through managed identity |
| Database jobs | `azurerm_container_app_job` | Manual migration, scheduled backup (`0 2 * * *` UTC), and manual restore jobs; all use the digest-pinned `database_ops_image` |
| Secrets | `azurerm_key_vault` + write-only secret attributes | PostgreSQL password and database connection string are generated/passed without persisting plaintext in Terraform state or `.tfvars`; purge protection and RBAC are enabled |
| Backups | `azurerm_storage_account` + private `backups` container | Blob versioning and 365-day soft delete; `backups/tradebook/` base blobs and versions retained for 2,555 days, moved to cool after 30 days; **no immutability/WORM policy** (D6) |
| Logs/alerts | `azurerm_log_analytics_workspace` + Azure Monitor | PostgreSQL, Key Vault, and Blob diagnostics; alerts for API health, PostgreSQL health/capacity, failed jobs, and a missing daily backup completion event |
| Identity | four `azurerm_user_assigned_identity` resources | Separate API, migration, backup, and restore identities; least-privilege ACR, Key Vault, and Blob roles |

Explicitly forbidden (previous spec's defects): plaintext `master_password` in variables/state defaults, `dev_password_123` anywhere, `0.0.0.0/0` database firewall rules, resources for Tiers 2/3, AWS providers.

The current infrastructure still carries the legacy local-HMAC `jwt-signing-key` until D15's Task 12 Entra cutover removes it. Task 07 must not expand that transitional dependency; production identity configuration is owned by Task 12.

Nightly backup uses custom-format, Zstandard-compressed `pg_dump`, validates the archive, emits a SHA-256 manifest, and uploads both files with managed-identity AzCopy to `backups/tradebook/YYYY/MM/`. The manual restore job validates the manifest, refuses an existing target database, restores into a fresh `tradebook_restore_*` database, reapplies/verifies migrations, and removes the rehearsal database by default. Detailed bootstrap, rollback, and restore operations live in `infra/terraform/README.md`.

## 6. CI/CD — GitHub Actions

- `ci.yml` (pull requests + pushes to `main`) always runs governance and generated-contract drift checks. Path-filtered backend and frontend jobs run their build/lint/test gates. The infrastructure job formats/validates Terraform, executes `verify-tier1.ps1`, syntax-checks every infrastructure shell script, builds both Docker targets, and runs the real Compose migration plus backup/restore rehearsal.
- `deploy.yml` runs only after a successful `main` CI push and requires the protected `production` GitHub environment plus an evidence pointer for its reviewer policy. It authenticates to Azure through OIDC, captures every deployed image for rollback, builds/pushes both images to ACR, resolves digest references, updates the three database jobs, waits for the migration job to succeed, deploys the API digest, and smoke-checks `/health/ready`. A later failure restores all prior image references and rechecks readiness; migrations remain forward-only.

## 7. Verification (executable)

| # | Check | Pass |
| :--- | :--- | :--- |
| V1 | Set `POSTGRES_PASSWORD`, then run `docker compose config --services` | Output is exactly one line: `postgres` |
| V2 | `docker compose up -d --wait postgres` | PostgreSQL reports healthy only after `schema_migrations` contains exactly one row per ordered migration file |
| V3 | Re-run `/opt/tradebook/database-ops/run-migrations.sh` with the local socket variables | Exit 0 with no duplicate application; a changed applied checksum makes the command fail |
| V4 | `terraform fmt -check -recursive infra/terraform && terraform -chdir=infra/terraform init -backend=false -input=false && terraform -chdir=infra/terraform validate` | Exit 0 |
| V5 | `pwsh -File infra/validation/verify-tier1.ps1` | D9 topology, forbidden-technology, state-safe secret, naming, image, and shared migration-contract checks pass |
| V6 | `bash -n infra/database-ops/*.sh infra/compose/postgres-init/*.sh infra/validation/*.sh && docker build --target database-ops --tag tradebook-database-ops:verify . && docker build --target runtime --tag tradebook-api:verify .` | Shell syntax and both image builds succeed |
| V7 | `POSTGRES_PASSWORD='<local-only>' bash infra/validation/verify-backup-restore.sh` | A dated dump + manifest are produced, restored into a fresh database, migrations validate, and source/restored `contracts` counts match |

No cost table is included by design: verify real pricing at implementation time; the previous spec's cost figures were stale/incomplete (missing NAT, wrong HTTPS assumptions).

## 8. Anti-cheating rules

- `terraform validate` green is necessary but NOT sufficient — the repository-specific V5 policy validator and V7 backup/restore rehearsal must also pass.
- No `|| true`, no `-ErrorAction SilentlyContinue`-style suppression in any pipeline or verify script.
- The Compose file used for local and CI database operations MUST be the root file — no CI-only Compose variant or additional default service.
