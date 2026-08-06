# Open Questions & Verification Strategy — ⚠️ LEGACY (Iteration 1, SurrealDB-era)

Part of Tradebook architecture plan. Original as-planned testing strategy; see `review/engineering-and-product-gaps.md` for gaps found in it.

## 4. Open Questions / Assumptions

> [!NOTE]
> All primary architectural decisions resolved. Assumptions below guide initial project scaffold:
> 1. **Database Deployment**: SurrealDB runs standalone (Docker or Surreal Cloud), accessible via WebSockets.
> 2. **Authentication Issuer**: .NET backend handles login/registration, issues signed JWTs used by both React and SurrealDB.

---

## 5. Verification & Testing Strategy

### Automated Verification
* Frontend: unit/integration via Vitest & React Testing Library; E2E via Playwright.
* Backend: integration testing for FastEndpoints via `Microsoft.AspNetCore.Mvc.Testing` and xUnit.

### Manual Verification Flow
1. Verify Vite dev server launches cleanly, zero bundle warnings.
2. Connect React SPA to SurrealDB WebSocket endpoint, confirm live query subscriptions update on mutation.
3. Test JWT auth flow: login via .NET → receive JWT → pass to SurrealDB → assert RLS permissions filter data correctly.
4. Verify React Flow canvas node dragging + dnd-kit sidebar item dropping work without pointer conflict.
