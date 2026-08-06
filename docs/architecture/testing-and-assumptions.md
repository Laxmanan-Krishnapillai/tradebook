# Open Questions & Verification Strategy

> **LEGACY — DO NOT IMPLEMENT (marked 2026-08-06).** These assumptions target the abandoned Iteration 1 SurrealDB stack. Current verification rules live in [`decision-log.md`](decision-log.md) D10 and `tasks/README.md`.

*Part of the [Tradebook architecture plan](../README.md). This is the original as-planned testing strategy; see [review/engineering-and-product-gaps.md](../review/engineering-and-product-gaps.md) for gaps found in it.*

## 4. Open Questions / Assumptions

> [!NOTE]
> All primary architectural decisions have been resolved. The assumptions below will guide the initial project scaffold:
> 1. **Database Deployment**: SurrealDB will run as a standalone service (Docker or Surreal Cloud) accessible via WebSockets.
> 2. **Authentication Issuer**: .NET backend will handle user login/registration and issue signed JWTs used by both React and SurrealDB.

---

## 5. Verification & Testing Strategy

### Automated Verification
* **Frontend**: Unit & integration tests via Vitest & React Testing Library; end-to-end tests via Playwright.
* **Backend**: Integration testing for FastEndpoints using `Microsoft.AspNetCore.Mvc.Testing` and xUnit.

### Manual Verification Flow
1. Verify Vite dev server launches cleanly with zero bundle warnings.
2. Connect React SPA to SurrealDB WebSocket endpoint and confirm live query subscriptions update on mutation.
3. Test JWT authentication flow: Login via .NET -> Receive JWT -> Pass to SurrealDB -> Assert RLS permissions filter data correctly.
4. Verify React Flow canvas node dragging and dnd-kit sidebar item dropping work without pointer conflict.
