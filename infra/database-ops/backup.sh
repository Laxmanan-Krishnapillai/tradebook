#!/usr/bin/env bash
set -Eeuo pipefail

: "${PGHOST:?PGHOST is required}"
: "${PGDATABASE:?PGDATABASE is required}"
: "${PGUSER:?PGUSER is required}"
BACKUP_OUTPUT_DIRECTORY="${BACKUP_OUTPUT_DIRECTORY:-}"
if [[ -n "$BACKUP_OUTPUT_DIRECTORY" ]]; then
  if [[ ! "$BACKUP_OUTPUT_DIRECTORY" =~ ^/[A-Za-z0-9._/-]+$ ]]; then
    echo "BACKUP_OUTPUT_DIRECTORY must be an absolute trusted path." >&2
    exit 1
  fi
else
  : "${AZURE_STORAGE_ACCOUNT:?AZURE_STORAGE_ACCOUNT is required}"
  : "${AZURE_STORAGE_CONTAINER:?AZURE_STORAGE_CONTAINER is required}"
  : "${AZURE_CLIENT_ID:?AZURE_CLIENT_ID is required}"
fi

work_directory="$(mktemp -d)"
trap 'rm -rf "$work_directory"' EXIT

timestamp="$(date -u +'%Y-%m-%dT%H-%M-%SZ')"
year="${timestamp:0:4}"
month="${timestamp:5:2}"
dump_name="tradebook-${timestamp}.dump"
dump_path="$work_directory/$dump_name"
manifest_path="$dump_path.sha256"
blob_prefix="tradebook/$year/$month"

pg_dump \
  --host="$PGHOST" \
  --port="${PGPORT:-5432}" \
  --username="$PGUSER" \
  --dbname="$PGDATABASE" \
  --format=custom \
  --compress=zstd:6 \
  --no-owner \
  --no-acl \
  --file="$dump_path"

test -s "$dump_path"
pg_restore --list "$dump_path" >/dev/null
(
  cd "$work_directory"
  sha256sum "$dump_name" > "${dump_name}.sha256"
)

if [[ -n "$BACKUP_OUTPUT_DIRECTORY" ]]; then
  destination_directory="$BACKUP_OUTPUT_DIRECTORY/$blob_prefix"
  mkdir -p "$destination_directory"
  test ! -e "$destination_directory/$dump_name"
  test ! -e "$destination_directory/${dump_name}.sha256"
  install --mode=0600 "$dump_path" "$destination_directory/$dump_name"
  install --mode=0600 "$manifest_path" "$destination_directory/${dump_name}.sha256"
else
  storage_url="https://${AZURE_STORAGE_ACCOUNT}.blob.core.windows.net/${AZURE_STORAGE_CONTAINER}"
  export AZCOPY_AUTO_LOGIN_TYPE=MSI
  export AZCOPY_MSI_CLIENT_ID="$AZURE_CLIENT_ID"
  azcopy copy "$dump_path" "$storage_url/$blob_prefix/$dump_name" --from-to=LocalBlob --overwrite=false
  azcopy copy "$manifest_path" "$storage_url/$blob_prefix/${dump_name}.sha256" --from-to=LocalBlob --overwrite=false
fi

printf '{"event":"backup.completed","blob":"%s/%s","bytes":%s,"timestamp":"%s"}\n' \
  "$blob_prefix" "$dump_name" "$(stat --format='%s' "$dump_path")" "$timestamp"
