#!/usr/bin/env bash
set -euo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_directory/.." && pwd)"
terraform_directory="$repository_root/infra/terraform"
default_plan_file="$repository_root/reports/terraform-production.tfplan"

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    fail "required command '$1' is not available"
  fi
}

require_file() {
  [[ -f "$1" ]] || fail "required file does not exist: $1"
}

usage() {
  cat <<'USAGE'
Usage: scripts/production-runbook.sh <command>

Commands:
  preflight          Validate local tooling, repository state, Terraform, and compose.
  plan               Create a reviewed Azure production Terraform plan.
  apply              Apply an existing plan after explicit production confirmation.
  show-migrations    Print the ordered migration files for the release record.
  verify             Run the full platform verification protocol.
  rehearse-backup    Restore BACKUP_PATH and compare it with DATABASE_URL.

Environment for plan:
  TF_BACKEND_CONFIG  Path to the production backend configuration file.
  TF_VARS_FILE       Path to the production variable file.
  TF_PLAN_FILE       Optional output path; defaults under reports/.

Environment for apply:
  TF_PLAN_FILE                    Plan produced by the plan command.
  TRADEBOOK_PRODUCTION_CONFIRM    Must equal APPLY_REVIEWED_PLAN.

The reviewed migration runner and the Azure backup-retention contract remain explicit
production blockers in docs/architecture/spec-issues.md. This helper does not improvise
either contract and does not provision Caddy outside the Task 07 Azure topology.
USAGE
}

preflight() {
  for command_name in az docker dotnet git npm psql terraform; do
    require_command "$command_name"
  done

  cd "$repository_root"
  git diff --quiet
  git diff --cached --quiet
  untracked_files="$(git ls-files --others --exclude-standard)"
  [[ -z "$untracked_files" ]] || fail "production preflight requires a clean worktree"

  az account show --output none
  docker version >/dev/null
  terraform -chdir="$terraform_directory" init -backend=false -input=false
  terraform -chdir="$terraform_directory" fmt -check -recursive
  terraform -chdir="$terraform_directory" validate

  mapfile -t services < <(docker compose config --services)
  [[ ${#services[@]} -eq 2 ]] || fail "compose must define exactly postgres and api"
  [[ " ${services[*]} " == *" postgres "* ]] || fail "compose postgres service is missing"
  [[ " ${services[*]} " == *" api "* ]] || fail "compose api service is missing"

  echo "Production preflight PASSED. Review unresolved specification issues before planning."
}

plan() {
  require_command az
  require_command terraform
  : "${TF_BACKEND_CONFIG:?TF_BACKEND_CONFIG is required}"
  : "${TF_VARS_FILE:?TF_VARS_FILE is required}"
  require_file "$TF_BACKEND_CONFIG"
  require_file "$TF_VARS_FILE"

  plan_file="${TF_PLAN_FILE:-$default_plan_file}"
  mkdir -p "$(dirname "$plan_file")"
  az account show --output none
  terraform -chdir="$terraform_directory" init \
    -reconfigure \
    -input=false \
    -backend-config="$TF_BACKEND_CONFIG"
  terraform -chdir="$terraform_directory" fmt -check -recursive
  terraform -chdir="$terraform_directory" validate
  terraform -chdir="$terraform_directory" plan \
    -input=false \
    -var-file="$TF_VARS_FILE" \
    -out="$plan_file"
  terraform -chdir="$terraform_directory" show "$plan_file"
  echo "Terraform plan created at $plan_file. Obtain independent review before apply."
}

apply_plan() {
  require_command az
  require_command terraform
  : "${TF_PLAN_FILE:?TF_PLAN_FILE is required}"
  require_file "$TF_PLAN_FILE"
  [[ "${TRADEBOOK_PRODUCTION_CONFIRM:-}" == "APPLY_REVIEWED_PLAN" ]] || \
    fail "set TRADEBOOK_PRODUCTION_CONFIRM=APPLY_REVIEWED_PLAN after reviewing the plan"

  az account show --output none
  terraform -chdir="$terraform_directory" apply -input=false "$TF_PLAN_FILE"
  terraform -chdir="$terraform_directory" output
  echo "Reviewed Terraform plan applied. Continue with the reviewed migration job, then verify."
}

show_migrations() {
  found=false
  for migration in "$repository_root"/src/Database/Migrations/*.sql; do
    [[ -f "$migration" ]] || continue
    found=true
    basename "$migration"
  done
  [[ "$found" == true ]] || fail "no database migrations were found"
}

if [[ $# -ne 1 ]]; then
  usage
  exit 2
fi

case "$1" in
  preflight)
    preflight
    ;;
  plan)
    plan
    ;;
  apply)
    apply_plan
    ;;
  show-migrations)
    show_migrations
    ;;
  verify)
    exec "$script_directory/platform-verify.sh"
    ;;
  rehearse-backup)
    exec "$script_directory/backup-restore-rehearsal.sh"
    ;;
  *)
    usage
    exit 2
    ;;
esac
