#!/usr/bin/env bash
set -Eeuo pipefail

: "${PGHOST:?PGHOST is required}"
: "${PGDATABASE:?PGDATABASE is required}"
: "${PGUSER:?PGUSER is required}"
# Unix-socket connections (PGHOST is a path) authenticate as the OS user; a
# password is only meaningful for TCP hosts.
if [[ "$PGHOST" != /* ]]; then
  : "${PGPASSWORD:?PGPASSWORD is required for tcp connections}"
fi

connection_string="${TRADEBOOK_DATABASE_CONNECTION_STRING:-Host=$PGHOST;Port=${PGPORT:-5432};Database=$PGDATABASE;Username=$PGUSER${PGPASSWORD:+;Password=$PGPASSWORD}}"

exec dotnet /opt/tradebook/migrator/Tradebook.Migrations.dll \
  "$connection_string"
