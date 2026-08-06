# Reactive Backend-as-Database - Convex / InstantDB

*Part of [Alternative Architecture Exploration](README.md).*

### 7.1 Reactive backend-as-database — Convex / InstantDB

One system: server functions only write path, queries auto-subscribe and push diffs to clients — no separate DB + separate REST backend. Canvas nodes and kanban cards become documents. Known failure mode: committing every drag-frame as mutation, causing re-render storms across subscribers; real pattern used by CRDT-canvas tools is ephemeral presence channel for live drag, with one durable mutation on drop. Convex well-funded, SOC2-audited, self-hostable. InstantDB much smaller team with reportedly higher query latency (200-500ms) and no webhooks/cron yet. Both mean rewriting `.NET` layer entirely in TypeScript, losing SQL-depth reporting, no published enterprise data-residency guarantees. **Not for teams with real `.NET` investment, BI/reporting needs, or compliance requirements** — which this plan currently has (Section 2B).
