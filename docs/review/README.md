# Independent Architecture Review

*Part of the [Tradebook architecture plan](../README.md).*

> [!WARNING]
> The rest of this document frames all major decisions as "resolved." This section is an independent critique pass, including research into real-world SurrealDB production usage, that found one correctness bug in the plan as written and several design choices that carry materially more risk than "resolved" implies. Treat these as open decisions the team should explicitly re-confirm, not nitpicks.

## Files in this folder

| File | Covers |
|---|---|
| [access-control-and-data-model.md](access-control-and-data-model.md) | The `$auth`/`$token` correctness bug, the direct-DB-access risk and its resolved read-only pattern, multi-tenancy isolation, JWT revocation |
| [backend-and-jobs.md](backend-and-jobs.md) | Hangfire's second-datastore requirement |
| [frontend-state-and-ui.md](frontend-state-and-ui.md) | The missing TanStack Query dependency, animation library placement, React Flow + dnd-kit friction, and the full frontend read/write (CQRS) implementation pattern |
| [performance-and-scalability.md](performance-and-scalability.md) | What actually drives perceived "buttery smooth" performance, ranked |
| [surrealdb-production-readiness.md](surrealdb-production-readiness.md) | SurrealDB's production maturity, stability history, and licensing |
| [agent-readiness.md](agent-readiness.md) | Verification-loop ranking for AI-agent-driven changes, plus concrete tooling recommendations |
| [engineering-and-product-gaps.md](engineering-and-product-gaps.md) | Testing depth, CI/CD, error contracts, folder-structure governance, auth lifecycle, accessibility, undo/redo, workflow versioning, bundle size, observability |
| [action-items.md](action-items.md) | The master summary table — every finding, one row each |
| [adversarial-tasklist-review.md](adversarial-tasklist-review.md) | Adversarial critique of the 10-task master breakdown — every task rated Unsound; systemic contract/path/SLA failures and rules for the bootstrap rewrite |

Start with [action-items.md](action-items.md) for the fastest overview, then drill into whichever file covers the area you care about.
