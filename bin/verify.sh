#!/usr/bin/env bash
# bin/verify.sh — single full-gate entry point for tradebook.
#
# Mirrors .github/workflows/ci.yml (backend + contracts + frontend jobs) so that
# "green locally" == "green in CI". Exit 0 only if every SELECTED gate passes.
# This is the definition-of-done command referenced by AGENTS.md and the Codex
# kickoff prompt: an agent must get exit 0 here before declaring a task complete.
#
# Usage:
#   bin/verify.sh                 # all gates (format/analyzers/build/tests/contracts/frontend)
#   bin/verify.sh --backend-only  # backend gates only
#   bin/verify.sh --frontend-only # frontend gates only
#   bin/verify.sh --fast          # skip integration + mutation (quick inner loop)
#   bin/verify.sh --no-mutation   # skip Stryker
#   bin/verify.sh --no-integration# skip Testcontainers integration tests
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT" || exit 1

DO_BACKEND=1; DO_CONTRACTS=1; DO_FRONTEND=1; DO_INTEGRATION=1; DO_MUTATION=1
for arg in "$@"; do
  case "$arg" in
    --backend-only)    DO_CONTRACTS=0; DO_FRONTEND=0 ;;
    --frontend-only)   DO_BACKEND=0; DO_CONTRACTS=0; DO_INTEGRATION=0; DO_MUTATION=0 ;;
    --no-integration)  DO_INTEGRATION=0 ;;
    --no-mutation)     DO_MUTATION=0 ;;
    --fast)            DO_INTEGRATION=0; DO_MUTATION=0 ;;
    -h|--help)         sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "unknown option: $arg" >&2; exit 2 ;;
  esac
done

step() { printf '\n\033[1;34m==== %s ====\033[0m\n' "$*"; }
fail() { printf '\n\033[1;31mGATE FAILED: %s\033[0m\n' "$*" >&2; exit 1; }
have() { command -v "$1" >/dev/null 2>&1; }
STRYKER_USED_CONTAINER=0

run_stryker() {
  if dotnet --info >/dev/null 2>&1; then
    dotnet stryker --config-file stryker-config.json
    return
  fi

  have docker && docker info >/dev/null 2>&1 || return 1
  local docker_root="$ROOT"
  if [ "${OS:-}" = "Windows_NT" ]; then
    have cygpath || return 1
    docker_root="$(cygpath --windows "$ROOT")"
  fi

  echo "Local dotnet --info failed; running Stryker in the pinned .NET SDK container."
  STRYKER_USED_CONTAINER=1
  MSYS_NO_PATHCONV=1 docker run --rm \
    --env DOTNET_ROLL_FORWARD=Major \
    --volume "$docker_root:/workspace" \
    --workdir /workspace \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    bash -lc 'dotnet tool restore && dotnet stryker --config-file stryker-config.json'
}

have dotnet || fail "dotnet SDK not found on PATH"
SLN="src/Backend/Tradebook.sln"

if [ "$DO_BACKEND" = 1 ]; then
  step "restore tools + solution"
  dotnet tool restore || fail "dotnet tool restore"
  dotnet restore "$SLN" || fail "restore solution"
  dotnet restore tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj || fail "restore architecture tests"

  step "formatting (CSharpier)"
  dotnet tool run csharpier check . || fail "CSharpier formatting check"

  step "banned API analyzer negative compile probe"
  bash bin/check-banned-api.sh || fail "RS0030 banned API probe"

  step "build (warnings as errors)"
  dotnet build "$SLN" -c Debug --no-restore -warnaserror || fail "build -warnaserror"

  step "unit tests"
  dotnet test tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj --no-restore || fail "unit tests"

  if [ "$DO_INTEGRATION" = 1 ]; then
    if have docker && docker info >/dev/null 2>&1; then
      step "integration tests (Testcontainers / PostgreSQL 17)"
      dotnet test tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj --no-restore || fail "integration tests"
    else
      fail "Docker is not running — integration tests need it. Start Docker (a real done-check must include integration tests)."
    fi
  fi

  step "architecture tests"
  dotnet test tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj --no-restore || fail "architecture tests"

  if [ "$DO_MUTATION" = 1 ]; then
    step "mutation tests (Stryker, break=80)"
    run_stryker || fail "stryker mutation gate"
    if [ "$STRYKER_USED_CONTAINER" = 1 ]; then
      dotnet restore "$SLN" || fail "restore host assets after containerized Stryker"
    fi
  fi
fi

if [ "$DO_CONTRACTS" = 1 ]; then
  step "contract generation + drift check"
  bash scripts/check-contract-drift.sh || fail "TypeSpec, generated client, or runtime contract drift"
fi

if [ "$DO_FRONTEND" = 1 ]; then
  have npm || fail "npm not found on PATH"
  step "frontend lint + build + tests"
  (
    cd src/Frontend
    if [ ! -d node_modules ]; then npm ci; fi
    npm run lint && npm run knip && npm run build && npm test -- --run
  ) || fail "frontend gates"
fi

printf '\n\033[1;32m✔ ALL SELECTED GATES PASSED\033[0m\n'
