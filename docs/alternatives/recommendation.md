# Ranked Recommendation

*Part of [Alternative Architecture Exploration](README.md).*

### 7.9 Ranked recommendation (updated)

This is now two separable decisions, not one — worth keeping apart rather than bundling into a single go/no-go.

**Decision A — a small, low-risk, additive bet: pilot TanStack DB directly on the existing SurrealDB setup.** Using the confirmed community SurrealDB collection (§7.8) or the Query Collection's incremental-write API, TanStack DB can upgrade §6.11's hand-rolled optimistic-mutation/cache-merge pattern with a real incremental-join query engine and a battle-tested-*pattern* (if not yet battle-tested-*library*) optimistic-mutation API — with zero database or backend migration. This is the recommended starting point: cheap, reversible, no architectural blast radius, and it directly targets the live-join/aggregate needs this app actually has (kanban tags, workflow node/edge relationships). Given its beta status and ~70KB bundle cost, pilot on one view (the kanban board) before adopting app-wide.

**Decision B — the bigger bet: migrate off SurrealDB to Postgres, for the "zero network wait" performance ceiling.** If Decision A's pilot reveals real limits SurrealDB itself can't clear (e.g. the §6.10 live-query scaling soft spots, or the §6.9 production-maturity concerns), the two credible Postgres-based paths are:
- **PowerSync (§7.2)** — the more production-proven option, with a turnkey write-queue/connector built in, at the cost of the more restrictive Fair Source License and no native support for complex client-side joins.
- **Electric + TanStack DB (§7.7, §7.8)** — more powerful (real incremental joins) and more permissively licensed (Apache 2.0), but immature as a pairing (TanStack DB has no 1.0 yet) and pushes all shape-level authorization onto a hand-built auth proxy with no rules DSL at all.

Independent of both decisions: correct the implicit CRDT assumption per §7.3 (build debounced local-state + server writes now, not CRDT infrastructure), and treat §7.1, §7.4, and §7.5 as not worth pursuing for this specific product today.

**Decision B is a genuine fork, not a foregone conclusion** — it trades SurrealDB's live-query/multi-model ergonomics (and everything already built around it in Sections 1-3 and §6) for Postgres's maturity and a higher local-first performance ceiling. Worth a deliberate go/no-go conversation with the team before committing either way. Decision A, by contrast, is low-stakes enough to just try.
