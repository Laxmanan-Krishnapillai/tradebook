# Design Review Principles

Score each category from 0 (blocking) to 2 (strong), cite concrete selectors, and
finish with prioritized fixes.

1. **Hierarchy:** one obvious primary action, restrained chrome, and clear grouping.
2. **Density:** information-rich trading views use consistent 28px rows without
   sacrificing scanning or hit targets.
3. **Typography:** numeric data is tabular; labels, values, and metadata have distinct
   hierarchy.
4. **State:** loading, empty, error, focus, hover, and live-update states are explicit
   and do not shift layout.
5. **Accessibility:** keyboard order is complete, focus is visible, contrast holds,
   and meaning never depends on color alone.
6. **System fidelity:** tokens and registry components replace one-off values and
   duplicated primitives.

Hallmark anti-slop pass: flag excessive cards, decoration without meaning, repeated
headings, placeholder copy, inconsistent radii, and gratuitous gradients or motion.

