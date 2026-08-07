#!/usr/bin/env bash
set -euo pipefail

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "ERROR: required command '$1' is not available." >&2
    exit 1
  fi
}

for command_name in docker pg_restore psql diff mktemp; do
  require_command "$command_name"
done

: "${DATABASE_URL:?DATABASE_URL must point to the source PostgreSQL database}"
: "${BACKUP_PATH:?BACKUP_PATH must point to the latest custom-format pg_dump}"

if [[ ! -f "$BACKUP_PATH" ]]; then
  echo "ERROR: backup file does not exist: $BACKUP_PATH" >&2
  exit 1
fi

# Fail before starting a container if the input is not a readable custom-format dump.
pg_restore --list "$BACKUP_PATH" >/dev/null

container_name="tradebook-restore-rehearsal-$$"
work_directory="$(mktemp -d)"

cleanup() {
  local exit_code=$?
  trap - EXIT INT TERM

  if docker container inspect "$container_name" >/dev/null 2>&1; then
    if ! docker rm --force "$container_name"; then
      echo "ERROR: failed to remove rehearsal container '$container_name'." >&2
      exit_code=1
    fi
  fi

  if ! rm -rf "$work_directory"; then
    echo "ERROR: failed to remove rehearsal work directory '$work_directory'." >&2
    exit_code=1
  fi

  exit "$exit_code"
}
trap cleanup EXIT INT TERM

echo "Starting isolated PostgreSQL 17 restore target..."
docker run --detach \
  --name "$container_name" \
  --env POSTGRES_PASSWORD=rehearsal \
  --publish 127.0.0.1::5432 \
  postgres:17 >/dev/null

ready=false
for _ in $(seq 1 60); do
  if docker exec "$container_name" pg_isready --username postgres --dbname postgres --quiet; then
    ready=true
    break
  fi
  sleep 1
done

if [[ "$ready" != true ]]; then
  echo "ERROR: restore target did not become ready within 60 seconds." >&2
  docker logs "$container_name"
  exit 1
fi
docker exec "$container_name" pg_isready --username postgres --dbname postgres

host_mapping="$(docker port "$container_name" 5432/tcp)"
restore_port="${host_mapping##*:}"
if [[ ! "$restore_port" =~ ^[0-9]+$ ]]; then
  echo "ERROR: could not determine the restore container port from '$host_mapping'." >&2
  exit 1
fi

restore_url="postgresql://postgres:rehearsal@127.0.0.1:${restore_port}/postgres"
server_version_num="$(psql "$restore_url" --no-psqlrc --tuples-only --no-align --command 'SHOW server_version_num;')"
if (( server_version_num < 170000 || server_version_num >= 180000 )); then
  echo "ERROR: restore target is PostgreSQL server_version_num=$server_version_num, expected 17.x." >&2
  exit 1
fi

echo "Restoring backup into PostgreSQL 17..."
pg_restore \
  --exit-on-error \
  --no-owner \
  --no-privileges \
  --dbname "$restore_url" \
  "$BACKUP_PATH"

count_sql=$(cat <<'SQL'
SELECT c.relname,
       (xpath('/row/cnt/text()',
              query_to_xml(format('SELECT count(*) AS cnt FROM %I.%I', n.nspname, c.relname),
                           false, true, '')))[1]::text::bigint AS rows
FROM pg_class AS c
JOIN pg_namespace AS n ON n.oid = c.relnamespace
WHERE c.relkind = 'r'
  AND n.nspname = 'public'
ORDER BY c.relname;
SQL
)

source_counts="$work_directory/source-counts.txt"
restored_counts="$work_directory/restored-counts.txt"
psql "$DATABASE_URL" --no-psqlrc --set ON_ERROR_STOP=1 --tuples-only --no-align \
  --command "$count_sql" >"$source_counts"
psql "$restore_url" --no-psqlrc --set ON_ERROR_STOP=1 --tuples-only --no-align \
  --command "$count_sql" >"$restored_counts"

diff --unified "$source_counts" "$restored_counts"
echo "Backup-restore rehearsal PASSED: every public table row count matches the source."
