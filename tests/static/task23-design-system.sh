#!/usr/bin/env bash
set -euo pipefail

root="src/Frontend/src"

rg -q -- '--color-background:' "$root/styles.css"
rg -q 'tabular-nums slashed-zero lining-nums' "$root/components/ui/numeric-cell.tsx"
! rg -n '#[0-9a-fA-F]{3,8}\b|\b[0-9]+px\b' "$root/components/ui"
! rg -n 'box-shadow|backdrop-blur|AnimatePresence|\blayout\b' "$root/components/grid" "$root/components/canvas" 2>/dev/null
! rg -n '\bmotion\.[a-z]' "$root"
rg -q 'LazyMotion.*strict' "$root/components/providers/motion-provider.tsx"
! rg -n 'Spinner|animate-spin' "$root/components/ui"
rg -q 'bufferTime\(this\.windowTimeMs\)' "$root/lib/streaming/eventBatcher.ts"
