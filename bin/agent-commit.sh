#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 3 ] || [ "$#" -gt 4 ]; then
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

git_dir="$(git rev-parse --git-dir)"
message_file="$(mktemp "$git_dir/agent-commit.XXXXXX")"
trap 'rm -f "$message_file"' EXIT
printf '%s\n' "$message" > "$message_file"

npx --no-install commitlint --edit "$message_file"
git commit --file "$message_file"
