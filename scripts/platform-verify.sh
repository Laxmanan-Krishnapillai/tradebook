#!/usr/bin/env bash
set -euo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_directory/.." && pwd)"
cd "$repository_root"

mode="full"
if [[ $# -gt 1 ]]; then
  echo "Usage: $0 [--foundation-only]" >&2
  exit 2
fi
if [[ $# -eq 1 ]]; then
  if [[ "$1" != "--foundation-only" ]]; then
    echo "Usage: $0 [--foundation-only]" >&2
    exit 2
  fi
  mode="foundation"
fi

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    fail "required command '$1' is not available"
  fi
}

http_status() {
  curl --silent --show-error --output /dev/null --write-out '%{http_code}' "$@"
}

run_stryker() {
  if dotnet --info >/dev/null 2>&1; then
    dotnet stryker --config-file stryker-config.json
    return
  fi

  echo " -> Local dotnet --info failed; running Stryker in the pinned .NET SDK container."
  docker_repository_root="$repository_root"
  if [[ "${OS:-}" == "Windows_NT" ]]; then
    docker_repository_root="$(cygpath --windows "$repository_root")"
  fi

  MSYS_NO_PATHCONV=1 docker run --rm \
    --env DOTNET_ROLL_FORWARD=Major \
    --volume "$docker_repository_root:/workspace" \
    --workdir /workspace \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    bash -lc 'dotnet tool restore && dotnet stryker --config-file stryker-config.json'
}

for command_name in curl docker dotnet git node npm npx psql terraform; do
  require_command "$command_name"
done

: "${DATABASE_URL:?DATABASE_URL must point to the migrated Tradebook database}"
api_base_url="${API_BASE_URL:-http://localhost:8080}"
api_base_url="${api_base_url%/}"

echo "======================================================================"
echo "          TRADEBOOK PLATFORM VERIFICATION PROTOCOL"
echo "======================================================================"

echo "[1/8] Verifying PostgreSQL 17 schema, audit, and outbox invariants..."
server_version_num="$(psql "$DATABASE_URL" --no-psqlrc --tuples-only --no-align --command 'SHOW server_version_num;')"
if (( server_version_num < 170000 || server_version_num >= 180000 )); then
  fail "database server_version_num=$server_version_num, expected PostgreSQL 17.x"
fi

extension_count="$(psql "$DATABASE_URL" --no-psqlrc --tuples-only --no-align --command "SELECT count(*) FROM pg_extension WHERE extname = 'btree_gist';")"
[[ "$extension_count" == "1" ]] || fail "required btree_gist extension is missing"

sequence_column_count="$(psql "$DATABASE_URL" --no-psqlrc --tuples-only --no-align --command "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'outbox_events' AND column_name = 'sequence_id';")"
[[ "$sequence_column_count" == "1" ]] || fail "outbox_events.sequence_id is missing"

notify_trigger_count="$(psql "$DATABASE_URL" --no-psqlrc --tuples-only --no-align --command "SELECT count(*) FROM pg_trigger WHERE tgrelid = 'public.outbox_events'::regclass AND tgname = 'trg_outbox_notify' AND NOT tgisinternal;")"
[[ "$notify_trigger_count" == "1" ]] || fail "outbox notify trigger is missing"

audit_overlap_count="$(psql "$DATABASE_URL" --no-psqlrc --tuples-only --no-align --command "SELECT count(*) FROM audit_log AS a JOIN audit_log AS b ON a.entity_name = b.entity_name AND a.entity_id = b.entity_id AND a.audit_id < b.audit_id AND a.system_time && b.system_time AND a.valid_time && b.valid_time;")"
[[ "$audit_overlap_count" == "0" ]] || fail "audit_log contains $audit_overlap_count overlapping bi-temporal ranges"
echo " -> PostgreSQL invariants PASSED."

echo "[2/8] Verifying anonymous probes and JWT enforcement..."
live_status="$(http_status "$api_base_url/health/live")"
ready_status="$(http_status "$api_base_url/health/ready")"
delivery_status="$(http_status "$api_base_url/api/v1/deliveries")"
negotiate_status="$(http_status --request POST "$api_base_url/hubs/dashboard/negotiate?negotiateVersion=1")"
events_status="$(http_status "$api_base_url/api/v1/events?afterSequence=0&limit=1")"

[[ "$live_status" == "200" ]] || fail "/health/live returned HTTP $live_status, expected 200"
[[ "$ready_status" == "200" ]] || fail "/health/ready returned HTTP $ready_status, expected 200"
[[ "$delivery_status" == "401" ]] || fail "/api/v1/deliveries without JWT returned HTTP $delivery_status, expected 401"
[[ "$negotiate_status" == "401" ]] || fail "SignalR negotiate without JWT returned HTTP $negotiate_status, expected 401"
[[ "$events_status" == "401" ]] || fail "/api/v1/events without JWT returned HTTP $events_status, expected 401"
echo " -> Health and authentication contracts PASSED."

echo "[3/8] Building backend and running unit/integration suites..."
dotnet build src/Backend/Tradebook.sln -c Debug
dotnet test tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj --no-build
dotnet test tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj --no-build
echo " -> Backend verification PASSED."

echo "[4/8] Verifying realtime delivery and architecture boundaries..."
dotnet test tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj \
  --no-build --filter 'Category=RealTime'
dotnet test tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj
echo " -> Realtime and architecture verification PASSED."

echo "[5/8] Verifying generated contracts have zero drift..."
scripts/check-contract-drift.sh
echo " -> TypeSpec contract synchronization PASSED."

echo "[6/8] Verifying frontend build, tests, and lint..."
npm --prefix src/Frontend run build
npm --prefix src/Frontend test -- --run
npm --prefix src/Frontend run lint
echo " -> Frontend verification PASSED."

echo "[7/8] Verifying documentation, Terraform, and the Tier-1 compose topology..."
node scripts/verify-doc-links.mjs
terraform -chdir=infra/terraform init -backend=false -input=false
terraform -chdir=infra/terraform fmt -check -recursive
terraform -chdir=infra/terraform validate

mapfile -t compose_services < <(docker compose config --services)
[[ ${#compose_services[@]} -eq 2 ]] || fail "compose must define exactly postgres and api"
[[ " ${compose_services[*]} " == *" postgres "* ]] || fail "compose postgres service is missing"
[[ " ${compose_services[*]} " == *" api "* ]] || fail "compose api service is missing"
compose_images="$(docker compose config --images)"
[[ "$compose_images" == *"postgres:17"* ]] || fail "compose does not use postgres:17"
echo " -> Documentation, Terraform, and compose topology PASSED."

echo "[8/8] Verifying commit policy and mutation-test threshold..."
git log -1 --pretty=%B | npx commitlint
run_stryker
echo " -> Governance verification PASSED."

if [[ "$mode" == "foundation" ]]; then
  echo "======================================================================"
  echo " FOUNDATION VERIFICATION PASSED (Tasks 01-08)."
  echo " Task 09 Playwright and k6 gates were intentionally not executed."
  echo "======================================================================"
  exit 0
fi

task_09_status="$(awk -F'|' '/\*\*Task 09\*\*/ { value=$(NF-1); gsub(/^[[:space:]]+|[[:space:]]+$/, "", value); print value }' docs/tasks/README.md)"
[[ "$task_09_status" == "Implemented" ]] || fail "Task 09 is '$task_09_status'; full verification requires it to be Implemented"
: "${API_JWT:?API_JWT is required for the Task 09 k6 scenarios}"
require_command k6

echo "Running Task 09 Playwright suite..."
(
  cd tests/e2e
  npm ci
  npx playwright test --config=playwright.config.ts
)

echo "Running Task 09 k6 baseline-regression gates..."
(
  cd tests/performance
  BASE_URL="$api_base_url" API_JWT="$API_JWT" PROFILE=smoke k6 run k6/api-delivery-ingestion.js
  node compare-baseline.mjs api-delivery-ingestion
  BASE_URL="$api_base_url" API_JWT="$API_JWT" PROFILE=smoke k6 run k6/deliveries-read.js
  node compare-baseline.mjs deliveries-read
)

echo "======================================================================"
echo " SUCCESS: full Tradebook platform verification completed."
echo "======================================================================"
