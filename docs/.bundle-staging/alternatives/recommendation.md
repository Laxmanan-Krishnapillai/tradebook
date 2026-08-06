# Ranked Recommendation

*Part of [Alternative Architecture Exploration](README.md).*

### 7.9 Ranked recommendation (updated)

Now two separable decisions, not one — worth keeping apart rather than bundling into single go/no-go.

**Decision A — small, low-risk, additive bet: pilot TanStack DB directly on existing SurrealDB setup.** Using confirmed community SurrealDB collection (§7.8) or Query Collection's incremental-write API, TanStack DB can upgrade §6.11's hand-rolled optimistic-mutation/cache-merge pattern with real incremental-join query engine and battle-tested-*pattern* (if not yet battle-tested-*library*) optimistic-mutation API — with zero database or backend migration. Recommended starting point: cheap, reversible, no architectural blast radius, directly targets live-join/aggregate needs this app actually has (kanban tags, workflow node/edge relationships). Given beta status and ~70KB bundle cost, pilot on one view (kanban board) before adopting app-wide.

**Decision B — bigger bet: migrate off SurrealDB to Postgres, for "zero network wait" performance ceiling.** If Decision A's pilot reveals real limits SurrealDB itself can't clear (e.g. §6.10 live-query scaling soft spots, or §6.9 production-maturity concerns), two credible Postgres-based paths:
- **PowerSync (§7.2)** — more production-proven option, with turnkey write-queue/connector built in, at cost of more restrictive Fair Source License and no native support for complex client-side joins.
- **Electric + TanStack DB (§7.7, §7.8)** — more powerful (real incremental joins), more permissively licensed (Apache 2.0), but immature as pairing (TanStack DB has no 1.0 yet), pushes all shape-level authorization onto hand-built auth proxy with no rules DSL at all.

Independent of both decisions: correct implicit CRDT assumption per §7.3 (build debounced local-state + server writes now, not CRDT infrastructure), treat §7.1, §7.4, and §7.5 as not worth pursuing for this specific product today.

**Decision B is genuine fork, not foregone conclusion** — trades SurrealDB's live-query/multi-model ergonomics (and everything already built around it in Sections 1-3 and §6) for Postgres's maturity and higher local-first performance ceiling. Worth deliberate go/no-go conversation with team before committing either way. Decision A, by contrast, low-stakes enough to just try.
