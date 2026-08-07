# Specification issues

## 2026-08-07 â€” Task 05 catch-up response shape conflicts with Task 03

Task 05's catch-up blueprint treats `GET /api/v1/events` as an event array, while Task
03 returns `{ events, latestSequence }`. The frontend implements the Task 03 response
shape, processes `events` until a short page, and retains event-id deduplication.

## 2026-08-07 — Task 06 Tremor peer dependency contradicts React 19

Task 06 pins `@tremor/react@^3.18.0`, whose npm peer dependency accepts React 18 only, while the platform requires React 19. The installation therefore requires `--legacy-peer-deps`. Proposed resolution: either formally approve this verified peer override or replace Tremor with a React-19-compatible KPI component kit in a revised Task 06 specification.

## 2026-08-07 — Task 06 server-side dashboard validation has no backend contract

Task 06 requires a backend dashboard-save endpoint that validates `workspace_dashboards.layout_json`, but neither the Task 02 DTO/endpoints nor the current database model exposes a dashboard save contract. This frontend task cannot implement or test the required HTTP 400 behavior without inventing an endpoint. Proposed resolution: add the DTO, route, authorization policy, persistence mapping, and JSON-schema validation ownership to a backend task.
