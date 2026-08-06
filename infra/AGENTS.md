# Infrastructure Guide

- Local development uses plain PostgreSQL 17 through Docker Compose.
- Terraform targets Azure Tier 1 infrastructure only.
- Do not introduce Redis, NATS, TimescaleDB, ScyllaDB, or S3 WORM/Merkle components.
