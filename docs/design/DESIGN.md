# Tradebook Design System

This document is mandatory context for UI changes. Use Tailwind theme tokens and the
approved `@tradebook` registry components. Token lock is a merge-blocking rule.

## Primitive base

ShadCN components use the stable Base UI foundation (`@base-ui/react`) and the
`base-nova` registry style. Feature code consumes approved components rather than
importing Base UI directly; raw primitive imports belong only in owned UI and registry
components.

Application shortcuts use `@tanstack/react-hotkeys`; do not add document-level
`keydown` listeners. Use `Mod` for platform-aware chords, sequence hooks for route
navigation, and keep single-key/sequence shortcuts disabled in editable controls.
New form surfaces should compose `@tanstack/react-form` with the approved registry
fields when a form abstraction is needed; do not introduce another form state layer.

## Component sourcing and interaction target

Do not create a Tradebook component when a suitable component already exists. Check
the approved ShadCN-compatible source pool first: KokonutUI, SmoothUI, Skiper UI,
coss ui, HeroUI, React Bits, and OpenUI. Prefer an existing animated component when
motion communicates state. If none fits, compose the approved ShadCN Base UI primitive
with Motion, keeping the 100-200ms and reduced-motion rules below. Normalize adopted
source into the `@tradebook` registry before feature use.

Record-management screens target the dense, low-chrome, keyboard-first ease of use of
Twenty CRM and Linear: searchable relationship pickers instead of raw IDs, direct cell
editing where safe, click-through record sheets, preserved list context, discoverable
filter/sort/column controls, and efficient keyboard navigation. Final design QA must
compare the rendered screen with the selected reference at the same viewport and state,
then verify representative create, find, edit, relate, and domain-action flows end to
end. Visual similarity alone is not acceptance.

## Color tokens

| Token | Usage rule |
| --- | --- |
| `--color-brand-600` | Accent reserved for the single primary action in a view. |
| `--color-brand-900` | High-emphasis navigation and text, never decorative fill. |
| `--color-profit` | Positive P/L, always paired with `▲` and a signed value. |
| `--color-loss` | Negative P/L, always paired with `▼` and a signed value. |

## Spacing, density, and motion

Use Tailwind's named spacing scale. The default trading-grid row is `2.125rem` (34px),
matching the quiet-workspace redesign. Condensed rows are `1.75rem` (28px) and relaxed
rows are `2.5rem` (40px); density is expressed by a component variant, never an
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

- Default grid rows are 34px; condensed rows are 28px and relaxed rows are 40px.
  Numeric columns use `tabular-nums` and the mono type token.
- Profit and loss use a sign, `▲`/`▼`, and text in addition to color. Color is never
  the sole signal.
- Live prices and timestamps reserve width so updates never shift the layout.
- Every interactive element has a visible `focus-visible` ring and a complete
  keyboard path.
- One primary action per view uses the accent token.
- Compose `DataGrid`, `Combobox`, and `Toolbar` from the private registry. Do not
  import raw Base UI primitives from feature code.
