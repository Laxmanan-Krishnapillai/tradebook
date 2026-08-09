# Tradebook Design System

This document is mandatory context for UI changes. Use Tailwind theme tokens and the
approved `@tradebook` registry components. Token lock is a merge-blocking rule.

## Color tokens

| Token | Usage rule |
| --- | --- |
| `--color-brand-600` | Accent reserved for the single primary action in a view. |
| `--color-brand-900` | High-emphasis navigation and text, never decorative fill. |
| `--color-profit` | Positive P/L, always paired with `▲` and a signed value. |
| `--color-loss` | Negative P/L, always paired with `▼` and a signed value. |

## Spacing, density, and motion

Use Tailwind's named spacing scale. The default trading-grid row is `1.75rem` (28px).
Compact and comfortable density must be expressed by a component variant, not an
arbitrary utility. Motion communicates state, lasts 100–200ms, respects
`prefers-reduced-motion`, and never moves a live-value column.

`u-density-override` is the **only** custom utility. It exists for a host-controlled
`--row-density-override` value at an integration boundary. No other custom class or
arbitrary Tailwind value is permitted.

## Do / don't

```tsx
// Don't
<div className="p-[7px] bg-[#ff0000]" />
// Do
<div className="p-2 bg-brand-600" />

// Don't
<div role="button" onClick={buy}>Buy</div>
// Do
<Button intent="primary" onClick={buy}>Buy</Button>
```

## Trading hard rules

- Default grid rows are 28px; numeric columns use `tabular-nums`.
- Profit and loss use a sign, `▲`/`▼`, and text in addition to color. Color is never
  the sole signal.
- Live prices and timestamps reserve width so updates never shift the layout.
- Every interactive element has a visible `focus-visible` ring and a complete
  keyboard path.
- One primary action per view uses the accent token.
- Compose `DataGrid`, `Combobox`, and `Toolbar` from the private registry. Do not
  import raw Base UI primitives from feature code.

