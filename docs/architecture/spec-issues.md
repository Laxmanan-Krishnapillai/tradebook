# Specification Issues

## 2026-08-06 — Task 01 auxiliary schema conflicts with the entity model

**Gap:** Task 01 and the blueprint require `audit_log`, `outbox_events`,
`custom_field_definitions`, and `semantic_models`, while the authoritative entity model
states that no tables may be invented outside its listed domain entities. The first two
are additionally required by decision-log D2/D13, but the latter two are not covered by
the decision log.

**Proposed resolution:** Treat `audit_log` and `outbox_events` as cross-cutting platform
infrastructure explicitly authorized by the decision log. Add `custom_field_definitions`
and `semantic_models` to the entity model before implementing them, or explicitly
authorize them as platform tables. This task will implement only the two D2/D13-required
infrastructure tables and defer the ambiguous custom/semantic tables.

## 2026-08-06 — Task 01 workbook import inputs are absent

**Gap:** Task 01 requires a repeatable seed and Excel import pipeline for five named
workbooks, but none of those workbooks, sample rows, or mapping file exists in this
repository.

**Proposed resolution:** Supply sanitized workbook fixtures (or a complete mapping and
fixture dataset) under a versioned test-fixtures location. The schema migrations include
the required natural keys and intentionally do not seed invented business data.
