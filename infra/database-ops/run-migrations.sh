#!/usr/bin/env bash
set -Eeuo pipefail

: "${PGDATABASE:?PGDATABASE is required}"
: "${PGHOST:?PGHOST is required}"
: "${PGUSER:?PGUSER is required}"

migrations_directory="${MIGRATIONS_DIRECTORY:-/opt/tradebook/migrations}"
if [[ ! "$migrations_directory" =~ ^/[A-Za-z0-9._/-]+$ ]]; then
  echo "Migration directory must be an absolute trusted path." >&2
  exit 1
fi
if [[ ! -d "$migrations_directory" ]]; then
  echo "Migration directory does not exist: $migrations_directory" >&2
  exit 1
fi

mapfile -t migration_files < <(find "$migrations_directory" -maxdepth 1 -type f -name '*.sql' -printf '%f\n' | LC_ALL=C sort)
if (( ${#migration_files[@]} == 0 )); then
  echo "No SQL migrations found in $migrations_directory" >&2
  exit 1
fi

plan_file="$(mktemp)"
trap 'rm -f "$plan_file"' EXIT

{
  printf '%s\n' '\set ON_ERROR_STOP on'
  printf '%s\n' "SELECT pg_advisory_lock(hashtextextended('tradebook-schema-migrations', 0));"
  printf '%s\n' 'CREATE TABLE IF NOT EXISTS schema_migrations ('
  printf '%s\n' '  version VARCHAR(255) PRIMARY KEY,'
  printf '%s\n' "  checksum_sha256 CHAR(64) NOT NULL CHECK (checksum_sha256 ~ '^[0-9a-f]{64}$'),"
  printf '%s\n' '  applied_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()'
  printf '%s\n' ');'
  printf '%s\n' 'DO $migration_ledger_upgrade$'
  printf '%s\n' 'BEGIN'
  printf '%s\n' "  IF EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid = 'schema_migrations'::regclass AND attname = 'name' AND NOT attisdropped)"
  printf '%s\n' "     AND NOT EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid = 'schema_migrations'::regclass AND attname = 'version' AND NOT attisdropped) THEN"
  printf '%s\n' '    ALTER TABLE schema_migrations RENAME COLUMN name TO version;'
  printf '%s\n' '  END IF;'
  printf '%s\n' "  IF EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid = 'schema_migrations'::regclass AND attname = 'sha256' AND NOT attisdropped)"
  printf '%s\n' "     AND NOT EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid = 'schema_migrations'::regclass AND attname = 'checksum_sha256' AND NOT attisdropped) THEN"
  printf '%s\n' '    ALTER TABLE schema_migrations RENAME COLUMN sha256 TO checksum_sha256;'
  printf '%s\n' '  END IF;'
  printf '%s\n' 'END'
  printf '%s\n' '$migration_ledger_upgrade$;'
  printf '%s\n' 'CREATE OR REPLACE FUNCTION pg_temp.assert_migration_checksum(migration_name TEXT, migration_sha256 TEXT)'
  printf '%s\n' 'RETURNS void LANGUAGE plpgsql AS $$'
  printf '%s\n' 'BEGIN'
  printf '%s\n' '  IF EXISTS (SELECT 1 FROM schema_migrations WHERE version = migration_name AND checksum_sha256 <> migration_sha256) THEN'
  printf '%s\n' "    RAISE EXCEPTION 'Applied migration % has a different checksum', migration_name;"
  printf '%s\n' '  END IF;'
  printf '%s\n' 'END;'
  printf '%s\n' '$$;'

  for migration_name in "${migration_files[@]}"; do
    if [[ ! "$migration_name" =~ ^[0-9]{3}_[a-z0-9_]+\.sql$ ]]; then
      echo "Invalid migration filename: $migration_name" >&2
      exit 1
    fi

    migration_path="$migrations_directory/$migration_name"
    migration_sha256="$(sha256sum "$migration_path" | awk '{print $1}')"
    printf '\\set migration_name %s\n' "$migration_name"
    printf '\\set migration_sha256 %s\n' "$migration_sha256"
    printf '%s\n' "SELECT pg_temp.assert_migration_checksum(:'migration_name', :'migration_sha256');"
    printf '%s\n' "SELECT NOT EXISTS (SELECT 1 FROM schema_migrations WHERE version = :'migration_name') AS apply_migration \\gset"
    printf '%s\n' '\if :apply_migration'
    printf '%s\n' 'BEGIN;'
    printf '\\ir %s\n' "$migration_path"
    printf '%s\n' "INSERT INTO schema_migrations (version, checksum_sha256) VALUES (:'migration_name', :'migration_sha256');"
    printf '%s\n' 'COMMIT;'
    printf '%s\n' '\endif'
  done

  printf '%s\n' "SELECT pg_advisory_unlock(hashtextextended('tradebook-schema-migrations', 0));"
} > "$plan_file"

psql \
  --no-psqlrc \
  --host="${PGHOST:-}" \
  --port="${PGPORT:-5432}" \
  --username="$PGUSER" \
  --dbname="$PGDATABASE" \
  --file="$plan_file"

echo "Applied and verified ${#migration_files[@]} Tradebook migrations."
