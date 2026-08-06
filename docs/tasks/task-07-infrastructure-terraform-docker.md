# Task 07: Tier-1 Infrastructure as Code (Terraform, Azure) & Local Docker Compose

> **REWRITTEN 2026-08-06** per [`architecture/decision-log.md`](../architecture/decision-log.md) **D9** (Tier 1 only) and **D14** (Azure, not AWS — the organization's tenant and existing production PostgreSQL are Azure; the previous spec's AWS target was never grounded in org reality, and its Aurora+TimescaleDB combination was impossible anyway). Tiers 2–3, NATS, TimescaleDB, Redis, ScyllaDB, and Salesforce integration are deleted. This file fully replaces the previous spec; filename kept for link stability.

- **Prerequisites**: Task 01 (migrations to run), Task 02 (API container)
- **Consumed by**: Task 09 (E2E env), Task 10 (deploy verification)
- **Complexity**: Medium

---

## 1. Scope

### In scope
1. `docker-compose.yml` for local dev + CI: `postgres` (17), `api`, optional `frontend` dev profile.
2. Terraform for one production environment on Azure: resource group, Container Apps, PostgreSQL Flexible Server 17, Blob storage (versioned backups per D6), Key Vault, Log Analytics.
3. GitHub Actions: CI (build/test, path-filtered) and CD (deploy image on `main`).
4. Nightly `pg_dump` backup job to versioned Blob storage.

### Out of scope
- Multi-region, autoscaling beyond ACA defaults, WAF/CDN (add when a growth signal exists — D9).
- Any message broker or cache service (D2; HybridCache is in-process, Task 02).

---

## 2. Repository layout (monorepo, decision-log D12)

```
infra/
  terraform/
    main.tf            # providers, backend, resource group
    database.tf        # PostgreSQL Flexible Server + database + firewall
    containerapp.tf    # ACA environment + api app + ingress
    storage.tf         # backup storage account (versioning + immutability policy)
    keyvault.tf        # secrets: pg admin password, JWT signing key
    monitoring.tf      # Log Analytics workspace + diagnostics
    variables.tf       # all inputs, no defaults containing secrets
    outputs.tf         # api_fqdn, postgres_fqdn, backup_storage_name
  compose/             # (root docker-compose.yml references this dir for init scripts)
    postgres-init/01-extensions.sql
docker-compose.yml     # repo root
.github/workflows/ci.yml
.github/workflows/deploy.yml
Dockerfile             # repo root — builds src/Backend, embeds built SPA
```

---

## 3. Local development — `docker-compose.yml` (repo root)

```yaml
services:
  postgres:
    image: postgres:17          # plain image — TimescaleDB removed per D3
    environment:
      POSTGRES_DB: tradebook
      POSTGRES_USER: tradebook
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?set in .env, never committed}
    ports: ["5432:5432"]
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./infra/compose/postgres-init:/docker-entrypoint-initdb.d:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U tradebook -d tradebook"]
      interval: 5s
      timeout: 3s
      retries: 10

  api:
    build: .
    depends_on:
      postgres: { condition: service_healthy }
    environment:
      ConnectionStrings__Tradebook: "Host=postgres;Database=tradebook;Username=tradebook;Password=${POSTGRES_PASSWORD}"
      Jwt__SigningKey: ${JWT_SIGNING_KEY:?set in .env}
    ports: ["8080:8080"]
    healthcheck:
      test: ["CMD", "wget", "-qO-", "http://localhost:8080/health/live"]
      interval: 10s
      timeout: 3s
      retries: 5

volumes:
  pgdata:
```

`infra/compose/postgres-init/01-extensions.sql`:

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;
-- gen_random_uuid() is built into PG 13+; uuid-ossp is NOT needed (D3 cleanup).
```

Rules:
- **No `nats`, `redis`, or any other service.** `tasks/README.md`'s verification uses `docker compose up -d postgres` only.
- There is exactly ONE compose file in the repo, at the root, owned by this task. Other tasks reference it; they never define their own.
- Frontend dev runs `npm run dev` directly (Vite, port 5173, proxy `/api` and `/hubs` to 8080) — no container needed locally.

## 4. Production image — `Dockerfile` (repo root)

Multi-stage: (1) `node:22` builds `src/Frontend` → `dist/`; (2) `mcr.microsoft.com/dotnet/sdk:9.0` publishes `src/Backend/Tradebook.sln` Release (JIT — no `PublishAot`, D7); (3) `mcr.microsoft.com/dotnet/aspnet:9.0` runtime, copies publish output and SPA `dist/` into `wwwroot/`, non-root user, `EXPOSE 8080`, entrypoint `dotnet Tradebook.Api.dll`. The API serves the SPA via static files + fallback routing (Task 02); ACA ingress terminates TLS, so no Caddy container is needed in production.

## 5. Terraform (azurerm ≥ 4.x)

Normative resource set — implementer fills standard boilerplate, but these choices are fixed:

| Concern | Resource | Fixed decisions |
| :--- | :--- | :--- |
| Database | `azurerm_postgresql_flexible_server` | PG **17**, `B_Standard_B2s` to start; `azurerm_postgresql_flexible_server_configuration` for `shared_preload_libraries = "pg_stat_statements"` only — **no timescaledb** (D3); private access or firewall allowing only ACA outbound IPs; `azurerm_postgresql_flexible_server_database` name `tradebook` |
| Compute | `azurerm_container_app` + `azurerm_container_app_environment` | Single app `tradebook-api`, min 1 / max 2 replicas, ingress external on 8080, secrets pulled from Key Vault via managed identity |
| Secrets | `azurerm_key_vault` + secrets | `pg-admin-password` (generated via `random_password`, never a variable default), `jwt-signing-key`. **No plaintext secrets in `.tfvars`, state is stored in an Azure Storage backend with versioning** |
| Backups | `azurerm_storage_account` + container `backups` | Blob **versioning enabled** + time-based retention/immutability policy ≥ 7 years (fulfils D6 "versioned bucket"; WORM/legal-hold deferred) |
| Logs | `azurerm_log_analytics_workspace` | ACA + Flexible Server diagnostics wired |
| Identity | `azurerm_user_assigned_identity` | ACA → Key Vault get/list secrets; ACR pull if ACR used |

Explicitly forbidden (previous spec's defects): plaintext `master_password` in variables/state defaults, `dev_password_123` anywhere, `0.0.0.0/0` database firewall rules, resources for Tiers 2/3, AWS providers.

Nightly backup: a Container Apps Job (cron `0 2 * * *`) running `postgres:17`'s `pg_dump` against Flexible Server, piping to `az storage blob upload` into `backups/` (dated filename). Restore procedure documented in `infra/terraform/README.md` and rehearsed in Task 10's verification.

## 6. CI/CD — GitHub Actions

- `ci.yml` (pull_request + push): path-filtered jobs — `backend` (`dotnet build src/Backend/Tradebook.sln -warnaserror; dotnet test tests/...`, service container `postgres:17` for integration tests), `frontend` (`npm ci && npm run build && npm test` in `src/Frontend`), `infra` (`terraform -chdir=infra/terraform init -backend=false && terraform validate && terraform fmt -check`).
- `deploy.yml` (push to `main`, after CI): build/push image (GHCR or ACR), `az containerapp update` to the new tag, then run migrations (`src/Database/Migrations` runner from Task 01) as a one-shot ACA Job, then smoke-check `GET https://<api_fqdn>/health/ready`. Deployment requires a manually-approved GitHub environment (human-on-the-loop rule from `review/adversarial-tasklist-review.md` §5).

## 7. Verification (executable)

| # | Check | Pass |
| :--- | :--- | :--- |
| V1 | `docker compose up -d postgres` then apply Task 01 migrations | Both healthy; migrations exit 0 |
| V2 | `docker compose up --build api` | `/health/live` and `/health/ready` return 200 |
| V3 | `terraform -chdir=infra/terraform init -backend=false && terraform validate` | Exit 0 |
| V4 | `terraform plan` with dummy-but-valid vars | Plan renders; grep of plan output finds **no** plaintext secret values |
| V5 | `grep -rn "timescaledb\|nats\|redis\|scylla" infra/ docker-compose.yml` | Zero matches |
| V6 | Backup job dry-run against local compose | Dated dump blob produced; restore into a fresh container succeeds and `SELECT count(*) FROM contracts` matches source |

No cost table is included by design: verify real pricing at implementation time; the previous spec's cost figures were stale/incomplete (missing NAT, wrong HTTPS assumptions).

## 8. Anti-cheating rules

- `terraform validate` green is necessary but NOT sufficient — V4's secret-leak grep and V5's dead-tech grep must also pass.
- No `|| true`, no `-ErrorAction SilentlyContinue`-style suppression in any pipeline or verify script.
- The compose file used in CI MUST be the root file — no CI-only compose variant.
