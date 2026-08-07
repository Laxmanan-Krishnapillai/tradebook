#!/usr/bin/env bash
set -Eeuo pipefail

: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required for the isolated Compose database}"

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repository_root"

compose_project="tradebook-v6-${GITHUB_RUN_ID:-$$}"
compose=(docker compose --project-name "$compose_project")
cleanup() {
  "${compose[@]}" down --volumes
}
trap cleanup EXIT

"${compose[@]}" up --detach --wait postgres

expected_migration_count="$(find src/Database/Migrations -maxdepth 1 -type f -name '*.sql' | wc -l)"
applied_migration_count="$("${compose[@]}" exec --no-TTY postgres psql \
  --username tradebook --dbname tradebook --tuples-only --no-align \
  --command 'SELECT count(*) FROM schema_migrations;')"
if [[ "$applied_migration_count" != "$expected_migration_count" || "$applied_migration_count" -lt 1 ]]; then
  echo "Compose migration count mismatch: expected=$expected_migration_count applied=$applied_migration_count" >&2
  exit 1
fi

"${compose[@]}" exec --no-TTY postgres psql \
  --username tradebook --dbname tradebook --set=ON_ERROR_STOP=1 <<'SQL'
INSERT INTO counterparties (id, name, shorthand)
VALUES ('00000000-0000-0000-0000-000000000701', 'CI backup counterparty', 'CIBACKUP');

INSERT INTO contracts (contract_name, counterparty_id, product_type, action)
VALUES ('CI-BACKUP-CONTRACT', '00000000-0000-0000-0000-000000000701', 'Gas', 'Buy');
SQL

source_contract_count="$("${compose[@]}" exec --no-TTY postgres psql \
  --username tradebook --dbname tradebook --tuples-only --no-align \
  --command 'SELECT count(*) FROM contracts;')"

"${compose[@]}" exec --no-TTY \
  -e PGHOST=/var/run/postgresql \
  -e PGDATABASE=tradebook \
  -e PGUSER=tradebook \
  -e BACKUP_OUTPUT_DIRECTORY=/tmp/tradebook-backup \
  postgres bash /opt/tradebook/database-ops/backup.sh

backup_blob="$("${compose[@]}" exec --no-TTY postgres \
  find /tmp/tradebook-backup/tradebook -type f -name '*.dump' -printf '%P\n')"
if [[ -z "$backup_blob" || "$(printf '%s\n' "$backup_blob" | wc -l)" -ne 1 ]]; then
  echo "Expected exactly one local backup dump, found: $backup_blob" >&2
  exit 1
fi
backup_blob="tradebook/$backup_blob"

"${compose[@]}" exec --no-TTY \
  -e PGHOST=/var/run/postgresql \
  -e PGUSER=tradebook \
  -e BACKUP_BLOB="$backup_blob" \
  -e BACKUP_INPUT_DIRECTORY=/tmp/tradebook-backup \
  -e RESTORE_DATABASE=tradebook_restore_ci \
  -e RESTORE_KEEP_DATABASE=true \
  postgres bash /opt/tradebook/database-ops/restore.sh

restored_contract_count="$("${compose[@]}" exec --no-TTY postgres psql \
  --username tradebook --dbname tradebook_restore_ci --tuples-only --no-align \
  --command 'SELECT count(*) FROM contracts;')"
if [[ "$source_contract_count" != "$restored_contract_count" || "$source_contract_count" -lt 1 ]]; then
  echo "Backup/restore contract count mismatch: source=$source_contract_count restored=$restored_contract_count" >&2
  exit 1
fi

"${compose[@]}" exec --no-TTY postgres dropdb \
  --username tradebook --maintenance-db postgres tradebook_restore_ci
printf 'Backup/restore verified: migrations=%s source_contracts=%s restored_contracts=%s\n' \
  "$applied_migration_count" "$source_contract_count" "$restored_contract_count"
