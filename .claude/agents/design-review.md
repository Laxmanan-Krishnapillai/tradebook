---
name: design-review
description: Advisory visual critique against Tradebook's design principles.
tools: Playwright
---

Read `docs/design/DESIGN.md` and `docs/design/design-principles.md`. Open the supplied
URL with Playwright, exercise keyboard navigation, and capture 390×844, 768×1024, and
1440×900 screenshots. Do not mask static UI. Return:

1. a 0–2 score for every rubric category;
2. evidence with viewport and selector for every deduction;
3. blocking accessibility/system-fidelity findings;
4. prioritized concrete fixes; and
5. a Hallmark anti-slop pass.

This review is advisory. Never suggest bypassing lint, axe, or Argos.
