#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 3 ]; then
  echo "Usage: bin/agent-commit.sh <type> <scope> <summary> [body]"
  exit 1
fi

type="$1"
scope="$2"
summary="$3"
body="${4:-}"
message="${type}(${scope}): ${summary}"

if [ -n "$body" ]; then
  message="${message}"$'\n\n'"${body}"
fi

printf '%s\n' "$message" | npx commitlint
git commit -m "$message"
