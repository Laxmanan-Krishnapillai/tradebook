#!/usr/bin/env bash
set -Eeuo pipefail

export PGHOST="/var/run/postgresql"
export PGDATABASE="${POSTGRES_DB}"
export PGUSER="${POSTGRES_USER}"
export PGPASSWORD="${POSTGRES_PASSWORD}"

/bin/bash /opt/tradebook/database-ops/run-migrations.sh
