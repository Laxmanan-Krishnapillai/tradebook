# Alternative Architecture Exploration

*Part of [Tradebook architecture plan](../README.md). Not bound by decisions in [architecture/](../architecture/) or [review/](../review/).*

> [!NOTE]
> Everything in this folder researched with explicit instruction to ignore prior decisions in plan and consider genuinely different ways to build same product: workflow/automation canvases (drag-connect nodes), kanban boards, real-time analytics widgets, optimized for low latency, "buttery smooth" interactivity, low memory use. Specific figures (latency numbers, download counts, storage caps) come from research agents' web search passes, not independently re-verified line-by-line — treat as directionally reliable, not citation-grade, spot-check anything you'd base final decision on.

## Files in this folder

| File | Covers |
|---|---|
| [reactive-backend-as-database.md](reactive-backend-as-database.md) | Convex / InstantDB — unified reactive backend-as-database, replacing DB + REST split entirely |
| [local-first-sync-engines.md](local-first-sync-engines.md) | PowerSync, ElectricSQL, and TanStack DB in depth — strongest alternatives found, including path that keeps SurrealDB |
| [crdt-collaboration.md](crdt-collaboration.md) | Yjs / Automerge / Liveblocks / PartyKit — why this app likely doesn't need CRDTs yet |
| [surrealdb-embedded-wasm.md](surrealdb-embedded-wasm.md) | SurrealDB's own embedded WASM mode as local-first client (not shippable today) |
| [edge-compute.md](edge-compute.md) | Cloudflare Workers + Durable Objects |
| [workflow-engine-alternatives.md](workflow-engine-alternatives.md) | Restate.dev as Hangfire alternative for workflow-execution engine specifically |
| [recommendation.md](recommendation.md) | Bottom line — two separable decisions, ranked, with recommended sequencing |

**Start with [recommendation.md](recommendation.md)** — frames everything else as either "Decision A" (small, low-risk, additive bet you can try now) or "Decision B" (bigger bet to revisit only if Decision A hits real wall).
