#!/usr/bin/env bash
# Proves SAFE-07 and the culture ban declarations: the repo-wide
# BannedApiAnalyzers configuration rejects DateTime.Now and provider-less
# decimal/double parsing with RS0030.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROBE_DIR="$(mktemp -d "$ROOT/.banned-api-probe.XXXXXX")"

cleanup() {
  case "$PROBE_DIR" in
    "$ROOT"/.banned-api-probe.*) rm -rf -- "$PROBE_DIR" ;;
    *) printf 'Refusing to remove unexpected probe path: %s\n' "$PROBE_DIR" >&2 ;;
  esac
}
trap cleanup EXIT HUP INT TERM

cat >"$PROBE_DIR/BannedApiProbe.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
EOF

cat >"$PROBE_DIR/Program.cs" <<'EOF'
_ = DateTime.Now;
ReadOnlySpan<char> chars = "1";
ReadOnlySpan<byte> bytes = "1"u8;
_ = decimal.Parse("1");
_ = decimal.Parse("1", System.Globalization.NumberStyles.Number);
_ = decimal.TryParse("1", out _);
_ = decimal.TryParse(chars, out _);
_ = decimal.TryParse(bytes, out _);
_ = double.Parse("1");
_ = double.Parse("1", System.Globalization.NumberStyles.Number);
_ = double.TryParse("1", out _);
_ = double.TryParse(chars, out _);
_ = double.TryParse(bytes, out _);
EOF

set +e
build_output="$(DOTNET_CLI_UI_LANGUAGE=en dotnet build "$PROBE_DIR/BannedApiProbe.csproj" --nologo --verbosity:minimal 2>&1)"
build_status=$?
set -e

if [ "$build_status" -eq 0 ]; then
  printf '%s\n' "$build_output" >&2
  printf 'Expected the banned-API probe to fail with RS0030, but it compiled.\n' >&2
  exit 1
fi

if ! grep -Eq 'error[[:space:]]+RS0030' <<<"$build_output"; then
  printf '%s\n' "$build_output" >&2
  printf 'The probe failed, but not with the required RS0030 banned-API error.\n' >&2
  exit 1
fi

rs0030_count="$({
  grep -E 'Program\.cs\([0-9]+,[0-9]+\): error RS0030:' <<<"$build_output" || true
} | sort -u | wc -l | tr -d '[:space:]')"
if [ "$rs0030_count" -ne 11 ]; then
  printf '%s\n' "$build_output" >&2
  printf 'Expected 11 distinct RS0030 diagnostics, received %s.\n' "$rs0030_count" >&2
  exit 1
fi

unexpected_errors="$(
  grep -E ':[[:space:]]+error([[:space:]]|:)' <<<"$build_output" |
    grep -Ev ':[[:space:]]+error[[:space:]]+(RS0030|CA1305|MA0011):' || true
)"
if [ -n "$unexpected_errors" ]; then
  printf '%s\n' "$build_output" >&2
  printf 'The probe produced errors in addition to the expected RS0030 failure.\n' >&2
  exit 1
fi

printf 'RS0030 negative compile probe passed.\n'
