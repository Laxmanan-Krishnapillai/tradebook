#!/usr/bin/env bash
# Regenerate the contract-owned artifacts and fail when the checkout changes.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GENERATED="$ROOT/src/Frontend/src/api/generated"
OPENAPI="$ROOT/docs/api/typespec/tsp-output/@typespec/openapi3/openapi.yaml"

before="$(find "$GENERATED" -type f -print0 2>/dev/null | sort -z | xargs -0 sha256sum 2>/dev/null || true)"

cd "$ROOT"
if [[ ! -d "$ROOT/node_modules/@typespec/compiler" ]]; then
  echo "@typespec/compiler is not installed; run 'npm ci' at the repo root first (a bare npx would fetch the unrelated 'tsp' package)." >&2
  exit 1
fi
npx tsp compile docs/api/typespec
PYTHON="$(command -v python3 || command -v python)"
"$PYTHON" scripts/compare-contract-dtos.py

cd "$ROOT/src/Frontend"
npm run api:generate

if ! grep -Eq '^openapi:[[:space:]]*["'\'']?3\.1\.' "$OPENAPI"; then
  echo "TypeSpec did not emit an OpenAPI 3.1 document at $OPENAPI" >&2
  exit 1
fi

cd "$ROOT"
after="$(find "$GENERATED" -type f -print0 | sort -z | xargs -0 sha256sum)"
if [[ "$before" != "$after" ]]; then
  echo "Generated API client was stale; run npm --prefix src/Frontend run api:generate and commit the result." >&2
  exit 1
fi

# CONTRACT-09: retaining either the old configuration, build target, or package
# would create a second contract source alongside TypeSpec.
if [[ -e "$ROOT/tgconfig.json" ]] \
  || rg -n 'GenerateTypeScriptContracts|PackageReference[^>]+TypeGen|PackageVersion[^>]+TypeGen|TypeGen\.Core' \
    "$ROOT/Directory.Build.targets" \
    "$ROOT/Directory.Packages.props" \
    "$ROOT/src/Backend" \
    "$ROOT/.config/dotnet-tools.json" >/dev/null 2>&1; then
  echo "The retired TypeGen contract pipeline is still present." >&2
  exit 1
fi

echo "TypeSpec OpenAPI 3.1 output and generated API client are in sync."
