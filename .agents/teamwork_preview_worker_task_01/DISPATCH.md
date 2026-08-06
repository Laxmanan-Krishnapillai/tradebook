## 2026-08-05T09:22:08Z
<USER_REQUEST>
You are Task 01 Specification Author.
Working directory: c:\Users\LaxmananKrishnapilla\tradebook\.agents\teamwork_preview_worker_task_01.
Create state files (BRIEFING.md, progress.md) first.

Task:
Read ORIGINAL_REQUEST.md and the survey in c:\Users\LaxmananKrishnapilla\tradebook\.agents\teamwork_preview_explorer_r3_1\analysis.md and c:\Users\LaxmananKrishnapilla\tradebook\.agents\teamwork_preview_explorer_r3_3\analysis.md.

Author the detailed task specification at c:\Users\LaxmananKrishnapilla\tradebook\tasks\task-01-database-and-timescaledb-setup.md.
Must cover:
- Title: Task 01: PostgreSQL 17 + TimescaleDB 2.15+ Bi-Temporal Audit & Core DDL Setup
- Objectives, Scope, Dependencies, Prerequisites
- Full PostgreSQL 17 DDL Schema statements (entities, audit_log, outbox_events, custom_field_definitions)
- Bi-temporal valid_time and system_time TSTZRANGE range columns with btree_gist exclusion constraints
- get_entity_state_as_of SQL time-travel function
- TimescaleDB hypertable partitioning parameters for trade_ticks
- Step-by-step implementation guide, file structure, code snippets, test plan, agent verification steps.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Write completion report to c:\Users\LaxmananKrishnapilla\tradebook\.agents\teamwork_preview_worker_task_01\handoff.md and notify parent when done.
</USER_REQUEST>
