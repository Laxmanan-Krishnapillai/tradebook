#!/usr/bin/env bash
# bin/check-test-integrity.sh — cheap tripwire against gaming the test gate.
#
# Policy (matches AGENTS.md): changing tests to match INTENTIONALLY changed
# behaviour is expected and allowed. Silently deleting, [Skip]-ing, commenting
# out, or weakening tests to force a green gate is NOT. This script flags
# SUSPICIOUS reductions (fewer test attributes, new skips, fewer assertions,
# deleted test files) and fails ONLY when no justification is present.
#
# Justification = a commit in <base>..HEAD carrying a trailer:
#     Test-Change: <one line reason>
# (or the env override TEST_CHANGE_JUSTIFIED=1).
#
# The HARD strength gate is Stryker mutation testing in bin/verify.sh; this is a
# fast structural check that also runs in CI (.github/workflows/test-integrity.yml).
#
# Usage: bin/check-test-integrity.sh [BASE_REF]   (default: merge-base with main)
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"; cd "$ROOT" || exit 1

BASE="${1:-}"
if [ -z "$BASE" ] || ! git cat-file -e "${BASE}^{commit}" 2>/dev/null; then
  BASE="$(git merge-base HEAD main 2>/dev/null || echo main)"
fi

is_test() { printf '%s' "$1" | grep -Eiq '(^tests/.*\.cs$)|(\.(test|spec)\.[jt]sx?$)'; }

mapfile -t changed < <(git diff --name-only "$BASE" -- . 2>/dev/null | while read -r f; do is_test "$f" && echo "$f"; done)
mapfile -t deleted < <(git diff --name-only --diff-filter=D "$BASE" -- . 2>/dev/null | while read -r f; do is_test "$f" && echo "$f"; done)

if [ "${#changed[@]}" -eq 0 ] && [ "${#deleted[@]}" -eq 0 ]; then
  echo "test-integrity: no test files changed vs ${BASE} — OK"; exit 0
fi

count() { # $1 = ref (empty = working tree), $2 = path, $3 = regex
  local content
  if [ -z "$1" ]; then content="$(cat "$2" 2>/dev/null)"; else content="$(git show "$1:$2" 2>/dev/null)"; fi
  printf '%s' "$content" | grep -Eio "$3" 2>/dev/null | grep -c . || true
}

ATTR='\[(fact|theory)'
SKIP='(skip[[:space:]]*=)|(\bxit\b)|(\bxdescribe\b)|(\.(skip|only)[[:space:]]*\()|(\[ignore\])'
ASSERT='(assert\.)|(\.should\()|(\bexpect\()'

suspicious=()
for f in "${changed[@]}"; do
  ba="$(count "$BASE" "$f" "$ATTR")";   ha="$(count '' "$f" "$ATTR")"
  bs="$(count "$BASE" "$f" "$SKIP")";   hs="$(count '' "$f" "$SKIP")"
  bx="$(count "$BASE" "$f" "$ASSERT")"; hx="$(count '' "$f" "$ASSERT")"
  reasons=""
  [ "$ha" -lt "$ba" ] && reasons="${reasons} removed-tests(${ba}->${ha})"
  [ "$hs" -gt "$bs" ] && reasons="${reasons} added-skips(${bs}->${hs})"
  [ "$hx" -lt "$bx" ] && reasons="${reasons} fewer-asserts(${bx}->${hx})"
  [ -n "$reasons" ] && suspicious+=("${f}:${reasons}")
done
for f in "${deleted[@]}"; do suspicious+=("${f}: test-file-deleted"); done

if [ "${#suspicious[@]}" -eq 0 ]; then
  echo "test-integrity: test files changed but only added/strengthened — OK"; exit 0
fi

justified=0
if git log "${BASE}..HEAD" --format='%B' 2>/dev/null | grep -Eiq '^[[:space:]]*Test-Change:[[:space:]]*\S'; then justified=1; fi
[ "${TEST_CHANGE_JUSTIFIED:-0}" = 1 ] && justified=1

echo "test-integrity: suspicious test reductions vs ${BASE}:"
printf '  - %s\n' "${suspicious[@]}"

if [ "$justified" = 1 ]; then
  echo "test-integrity: justification present (Test-Change trailer / override) — ALLOWED."
  echo "                (Stryker still independently enforces test strength.)"
  exit 0
fi

cat >&2 <<'MSG'

test-integrity: FAILED — these look like weakened/removed tests with no rationale.
If the change is legitimate (behaviour genuinely changed under this task), add a
commit trailer explaining it:

    Test-Change: <one line: what behaviour changed and why the test had to change>

Never delete, skip, or weaken a test just to make the gate pass.
MSG
exit 1
