# Reactive Backend-as-Database - Convex / InstantDB

*Part of [Alternative Architecture Exploration](README.md).*

### 7.1 Reactive backend-as-database — Convex / InstantDB

One system: server functions are the only write path, queries auto-subscribe and push diffs to clients — no separate DB + separate REST backend. Canvas nodes and kanban cards become documents. The known failure mode is committing every drag-frame as a mutation, causing re-render storms across subscribers; the real pattern used by CRDT-canvas tools is an ephemeral presence channel for live drag, with one durable mutation on drop. Convex is well-funded, SOC2-audited, self-hostable. InstantDB is a much smaller team with reportedly higher query latency (200-500ms) and no webhooks/cron yet. Both mean rewriting the `.NET` layer entirely in TypeScript, losing SQL-depth reporting, and no published enterprise data-residency guarantees. **Not for teams with real `.NET` investment, BI/reporting needs, or compliance requirements** — which this plan currently has (Section 2B).
