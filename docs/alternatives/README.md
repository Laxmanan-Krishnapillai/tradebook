# Alternative Architecture Exploration

*Part of the [Tradebook architecture plan](../README.md). Not bound by decisions made in [architecture/](../architecture/) or [review/](../review/).*

> [!NOTE]
> Everything in this folder was researched with an explicit instruction to ignore prior decisions in the plan and consider genuinely different ways to build the same product: workflow/automation canvases (drag-connect nodes), kanban boards, and real-time analytics widgets, optimized for low latency, "buttery smooth" interactivity, and low memory use. Specific figures (latency numbers, download counts, storage caps) come from research agents' web search passes and were not independently re-verified line-by-line — treat them as directionally reliable, not citation-grade, and spot-check anything you'd base a final decision on.

## Files in this folder

| File | Covers |
|---|---|
| [reactive-backend-as-database.md](reactive-backend-as-database.md) | Convex / InstantDB — a unified reactive backend-as-database, replacing the DB + REST split entirely |
| [local-first-sync-engines.md](local-first-sync-engines.md) | PowerSync, ElectricSQL, and TanStack DB in depth — the strongest alternatives found, including a path that keeps SurrealDB |
| [crdt-collaboration.md](crdt-collaboration.md) | Yjs / Automerge / Liveblocks / PartyKit — and why this app likely doesn't need CRDTs yet |
| [surrealdb-embedded-wasm.md](surrealdb-embedded-wasm.md) | SurrealDB's own embedded WASM mode as a local-first client (not shippable today) |
| [edge-compute.md](edge-compute.md) | Cloudflare Workers + Durable Objects |
| [workflow-engine-alternatives.md](workflow-engine-alternatives.md) | Restate.dev as a Hangfire alternative for the workflow-execution engine specifically |
| [recommendation.md](recommendation.md) | The bottom line — two separable decisions, ranked, with a recommended sequencing |

**Start with [recommendation.md](recommendation.md)** — it frames everything else as either "Decision A" (a small, low-risk, additive bet you can try now) or "Decision B" (a bigger bet to revisit only if Decision A hits a real wall).
