# Infrastructure Guide

- Read `docs/architecture/decision-log.md` before changing infrastructure.
- Root `docker-compose.yml` is the only Compose file and its default topology is plain
  PostgreSQL 17 only. Keep database tests on PostgreSQL 17 Testcontainers.
- Terraform targets one Azure Tier-1 environment under `infra/terraform`; do not add
  Redis, NATS, TimescaleDB, ScyllaDB, multi-region resources, or WORM/Merkle controls.
- Bind workload access through separate managed identities. Never add a secret value to
  Terraform input variables, resource attributes persisted in state, workflow YAML, or
  Compose source. Terraform secret writes use ephemeral values and write-only attributes.
- Production container variables are immutable digest references. Migration, backup,
  and restore scripts live in `infra/database-ops` and must remain fail-fast.
- Run `terraform fmt -check -recursive`, `terraform validate`,
  `pwsh infra/validation/verify-tier1.ps1`, and shell syntax checks after edits.
