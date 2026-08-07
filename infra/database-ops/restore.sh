#!/usr/bin/env bash
set -Eeuo pipefail

: "${PGHOST:?PGHOST is required}"
: "${PGUSER:?PGUSER is required}"
: "${BACKUP_BLOB:?BACKUP_BLOB is required}"
: "${RESTORE_DATABASE:?RESTORE_DATABASE is required}"
RESTORE_KEEP_DATABASE="${RESTORE_KEEP_DATABASE:-false}"
BACKUP_INPUT_DIRECTORY="${BACKUP_INPUT_DIRECTORY:-}"

if [[ -n "$BACKUP_INPUT_DIRECTORY" ]]; then
  if [[ ! "$BACKUP_INPUT_DIRECTORY" =~ ^/[A-Za-z0-9._/-]+$ ]]; then
    echo "BACKUP_INPUT_DIRECTORY must be an absolute trusted path." >&2
    exit 1
  fi
else
  : "${AZURE_STORAGE_ACCOUNT:?AZURE_STORAGE_ACCOUNT is required}"
  : "${AZURE_STORAGE_CONTAINER:?AZURE_STORAGE_CONTAINER is required}"
  : "${AZURE_CLIENT_ID:?AZURE_CLIENT_ID is required}"
fi

if [[ ! "$BACKUP_BLOB" =~ ^tradebook/[0-9]{4}/(0[1-9]|1[0-2])/tradebook-[0-9]{4}-(0[1-9]|1[0-2])-([0-2][0-9]|3[01])T([01][0-9]|2[0-3])-[0-5][0-9]-[0-5][0-9]Z\.dump$ ]]; then
  echo "BACKUP_BLOB does not match the trusted Tradebook backup path format." >&2
  exit 1
fi
if [[ ! "$RESTORE_DATABASE" =~ ^tradebook_restore_[a-z0-9_]+$ ]]; then
  echo "RESTORE_DATABASE must start with tradebook_restore_ and contain only lowercase letters, digits, and underscores." >&2
  exit 1
fi
if [[ "$RESTORE_KEEP_DATABASE" != "true" && "$RESTORE_KEEP_DATABASE" != "false" ]]; then
  echo "RESTORE_KEEP_DATABASE must be true or false." >&2
  exit 1
fi

database_exists="$(
  psql --no-psqlrc --host="$PGHOST" --port="${PGPORT:-5432}" --username="$PGUSER" --dbname=postgres --tuples-only --no-align --set=database_name="$RESTORE_DATABASE" <<'SQL'
SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'database_name');
SQL
)"
if [[ "$database_exists" != "f" ]]; then
  echo "Refusing to restore into existing database $RESTORE_DATABASE." >&2
  exit 1
fi

work_directory="$(mktemp -d)"
database_created=false
cleanup() {
  rm -rf "$work_directory"
  if [[ "$database_created" == "true" && "$RESTORE_KEEP_DATABASE" == "false" ]]; then
    dropdb --host="$PGHOST" --port="${PGPORT:-5432}" --username="$PGUSER" --maintenance-db=postgres --if-exists "$RESTORE_DATABASE"
  fi
}
trap cleanup EXIT

dump_name="${BACKUP_BLOB##*/}"
dump_path="$work_directory/$dump_name"
manifest_path="$dump_path.sha256"
if [[ -n "$BACKUP_INPUT_DIRECTORY" ]]; then
  install --mode=0600 "$BACKUP_INPUT_DIRECTORY/$BACKUP_BLOB" "$dump_path"
  install --mode=0600 "$BACKUP_INPUT_DIRECTORY/${BACKUP_BLOB}.sha256" "$manifest_path"
else
  storage_url="https://${AZURE_STORAGE_ACCOUNT}.blob.core.windows.net/${AZURE_STORAGE_CONTAINER}"
  export AZCOPY_AUTO_LOGIN_TYPE=MSI
  export AZCOPY_MSI_CLIENT_ID="$AZURE_CLIENT_ID"
  azcopy copy "$storage_url/$BACKUP_BLOB" "$dump_path" --from-to=BlobLocal --overwrite=false
  azcopy copy "$storage_url/${BACKUP_BLOB}.sha256" "$manifest_path" --from-to=BlobLocal --overwrite=false
fi
(
  cd "$work_directory"
  sha256sum --check "${dump_name}.sha256"
)
pg_restore --list "$dump_path" >/dev/null

createdb --host="$PGHOST" --port="${PGPORT:-5432}" --username="$PGUSER" --maintenance-db=postgres "$RESTORE_DATABASE"
database_created=true
pg_restore --host="$PGHOST" --port="${PGPORT:-5432}" --username="$PGUSER" --dbname="$RESTORE_DATABASE" --no-owner --no-acl --exit-on-error "$dump_path"

PGDATABASE="$RESTORE_DATABASE" /bin/bash "$(dirname "$0")/run-migrations.sh"
psql --no-psqlrc --host="$PGHOST" --port="${PGPORT:-5432}" --username="$PGUSER" --dbname="$RESTORE_DATABASE" --set=ON_ERROR_STOP=1 --command="SELECT count(*) FROM schema_migrations; SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public';"

printf '{"event":"restore.completed","blob":"%s","database":"%s","kept":%s}\n' "$BACKUP_BLOB" "$RESTORE_DATABASE" "$RESTORE_KEEP_DATABASE"
