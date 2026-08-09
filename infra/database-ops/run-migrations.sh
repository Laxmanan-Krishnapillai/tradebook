#!/usr/bin/env bash
set -Eeuo pipefail

: "${PGHOST:?PGHOST is required}"
: "${PGDATABASE:?PGDATABASE is required}"
: "${PGUSER:?PGUSER is required}"
: "${PGPASSWORD:?PGPASSWORD is required}"

connection_string="${TRADEBOOK_DATABASE_CONNECTION_STRING:-Host=$PGHOST;Port=${PGPORT:-5432};Database=$PGDATABASE;Username=$PGUSER;Password=$PGPASSWORD}"

exec dotnet /opt/tradebook/migrator/Tradebook.Migrations.dll \
  "$connection_string"
