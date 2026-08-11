# Tradebook Linear / Twenty redesign QA

- Source visual truth: `C:\Users\LaxmananKrishnapilla\Downloads\Tradebook Redesign-selection.png`
- Live application: authenticated development build at `http://127.0.0.1:5176`
- Verification date: 2026-08-10

## Verified in the real application

- Deliveries loads 48 seeded records and Capacity bookings loads 10 seeded records without an error banner.
- Opening and creating records uses the sourced coss/Base UI right drawer.
- The drawer has a 260ms right-to-left transition and a transparent modal dismiss layer.
- An outside press is handled by Base UI as `outside-press`, closes the drawer, and is intercepted before it can reach a table row.
- Book type uses the styled Base UI Select. Its portal renders above the drawer and the listbox is positioned below the trigger without overlap.
- Table selection uses the SmoothUI spring-path checkbox interaction adapted to Base UI. Checked and indeterminate states are styled and animated.
- Selecting or dragging table text does not open a record.
- Table headers and cells are centered.

## Automated evidence

- Focused drawer, checkbox, and grid tests: 2 files, 9 tests passed.
- Production frontend build: passed.
- Scoped frontend lint: passed.

## Remaining visual gate

The controlled in-app browser viewport is currently narrow enough to activate the mobile layout, while the supplied visual reference is a 1920px desktop composition. The live interactions above are verified, but a same-viewport reference comparison is still required before the overall redesign can honestly be marked visually complete.

final result: blocked
