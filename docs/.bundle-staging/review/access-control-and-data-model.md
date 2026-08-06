# Access Control & Data Model

*Part of the [architecture review](README.md).*

### 6.1 Correctness bug: the RLS/JWT example as written will not work

Section 2's permission example reads `$auth.tenant_id` and `$auth.role`. That only populates if frontend authenticates via `DEFINE ACCESS ... TYPE RECORD` (SurrealDB looks up real DB user record, fills `$auth` from its fields). Authenticating with raw, externally-issued JWT via `DEFINE ACCESS ... TYPE JWT` — i.e. "hand SurrealDB same JWT .NET issued," as Section 2 currently describes — populates `$token.*`, not `$auth.*`, and doesn't require or consume an `id` claim at all.

One of two things has to change before this ships:
- Rewrite every permission clause to reference `$token.tenant_id` / `$token.role`, or
- Adopt `TYPE RECORD ... WITH JWT`, requires real SurrealDB user record per person, with `tenant_id`/`role` fields kept in sync with whatever .NET treats as source of truth for those attributes.

**Not a stylistic choice — `TYPE RECORD` mandatory if any per-tenant/per-role restriction on client connections matters at all.** Plain `TYPE JWT` access method authenticates as system-level user, which SurrealDB's own docs describe as having access "equivalent to system users, which is above fine-grained permissions" — i.e. it **bypasses table `PERMISSIONS` clauses entirely**, regardless of what's written in them. If §6.2's read-only-direct pattern below is adopted, client-facing access method must be `TYPE RECORD`; generic externally-issued JWT cannot be scoped down to read-only via `PERMISSIONS` no matter how clauses are written.

Also unresolved: JWT key configuration. HS256 with shared secret simplest (.NET and SurrealDB hold same secret) but means secret must rotate in lockstep across two systems. RS256/JWKS lets SurrealDB fetch .NET's public keys and rotate without redeploying SurrealDB config, at cost of standing up/maintaining JWKS endpoint. Pick one explicitly.

*Source: [DEFINE ACCESS TYPE JWT](https://surrealdb.com/docs/surrealql/statements/define/access/jwt), [DEFINE ACCESS TYPE RECORD](https://surrealdb.com/docs/surrealql/statements/define/access/record)*

---

### 6.2 The central risk: direct client-to-SurrealDB access over the public internet

Section 1's architecture has browser connect straight to SurrealDB over `ws://`/`wss://` for "simple" CRUD and live queries, with SurrealDB's PERMISSIONS clauses as *only* authorization boundary on that path. Architecturally similar to Supabase/Firebase, but neither actually skips a gateway the way this plan does:

- **Firebase** ships dedicated emulator and `@firebase/rules-unit-testing` framework for unit-testing security rules pre-deploy, plus Cloud Audit Logging of every rule evaluation and App Check.
- **Supabase** exposes Postgres through PostgREST — generated REST layer, not raw wire-protocol connection — sitting in front of RLS.
- **Hasura** generates typed GraphQL API and supports allow-list of pre-approved operations per role. BFF by design.
- **PowerSync / ElectricSQL** interpose sync/replication layer: reads scoped by declarative sync rules, writes go through upload queue backend still validates. Client never gets raw connection to database.

SurrealDB gives `PERMISSIONS` and `DEFINE EVENT` and expects team to build testing, audit logging, rate limiting itself. SurrealDB's own security docs recommend exposing it exclusively to internal network, not public internet — which browser-facing WebSocket endpoint necessarily violates.

**Blast radius under XSS**: with fixed backend endpoints, compromised client can only replay finite, already-validated request set. With raw query access, it inherits entire SurrealQL surface — can construct novel queries and probe every table the JWT's role could theoretically touch. Attack surface becomes "everything SurrealQL can express," not "the endpoints we wrote."

**Dual write path**: `DEFINE EVENT` can replicate side effects (audit trails, notifications, search-index updates) for direct writes, but events skip permission checks and `ASYNC` events run on a polling interval (5s default), not instantly. Side effects now live in two places — backend code for the "complex" path, DB events for the "simple" path — and drift silently the first time someone adds a side effect to one and forgets the other.

**Original recommendation (superseded below)**: blanket-proxy everything through .NET backend. After team clarified priority is low-latency, buttery-smooth, low-memory dashboard, follow-up research pass (§6.11) found more precise middle ground keeping performance properties direct access meant to buy, without open-ended write exposure.

**Resolved recommendation: read-only direct access, write-only via backend.**
- Browser's direct SurrealDB connection authorized via `TYPE RECORD` access and granted **SELECT and LIVE SELECT only** — every client-facing table defaults to `PERMISSIONS NONE` and explicitly allowlists `select`:
  ```surrealql
  DEFINE TABLE project SCHEMAFULL
      PERMISSIONS
          FOR select WHERE tenant = $auth.tenant_id AND (owner = $auth.id OR $auth.role = 'admin')
          FOR create, update, delete NONE;
  ```
- **All writes** go exclusively through .NET backend, which holds its own privileged connection. SurrealDB's change-feed mechanism then fans resulting update out to subscribed browsers automatically — backend doesn't need to implement its own pub/sub relay for this to work.
- This closes §6.2 write-path attack surface (no data corruption, no injected records, no privilege escalation via client-crafted write) and "dual write path" business-logic problem (exactly one route into data — audit trails and side effects live in one place), while leaving live-query push latency untouched, because reads never leave the direct browser↔SurrealDB path.
- Real precedent: same shape as Supabase (anon/authenticated key + RLS for direct reads/Realtime, `service_role` reserved for privileged backend writes) and Hasura (`backend_only` mutations/Actions for writes, direct queries/subscriptions for reads).
- **Residual risk, do not skip**: SurrealDB's historical default was `PERMISSIONS FULL` on any table without explicit clause (CVSS 8.8, fixed in 1.0.1) — one table without explicit `NONE` reopens full write access. Recent CVEs (CVE-2025-11060, GHSA-hv6h-hc26-q48p) show `LIVE SELECT` can leak data because permissions are evaluated at subscribe-time, not notification-time, and via graph traversals materializing related records that bypass field-level filters. Mitigation: default every table to `PERMISSIONS NONE` explicitly, pin patched SurrealDB version, add CI check that fails build if any client-facing table grants `create`/`update`/`delete`.
- If team decides residual risk above still unacceptable, full backend-proxying remains fallback — §6.11 found it costs low single-digit milliseconds in same-region deployment, imperceptible against dashboard's stated performance goals. Not a performance-forced choice either way; it's a risk-tolerance choice with a clear low-cost fallback.

*Sources: [SurrealDB security best practices](https://surrealdb.com/docs/learn/security/best-practices/security-best-practices), [SurrealDB security troubleshooting](https://surrealdb.com/docs/surrealdb/security/troubleshooting), [DEFINE ACCESS record](https://surrealdb.com/docs/surrealql/statements/define/access/record), [Permissions & RLS](https://surrealdb.com/docs/learn/security/authorization/permissions-and-row-level-security), [Default FULL permissions advisory](https://github.com/surrealdb/surrealdb/security/advisories/GHSA-x5fr-7hhj-34j3), [LIVE query exposure CVE-2025-11060](https://advisories.gitlab.com/cargo/surrealdb/CVE-2025-11060/), [Supabase RLS docs](https://supabase.com/docs/guides/database/postgres/row-level-security), [Hasura backend-only mutations/Actions](https://hasura.io/docs/2.0/actions/derive/), [Hasura allow-list](https://hasura.io/docs/2.0/security/allow-list/)*

---

### 6.3 Multi-tenancy: row-level tenant column vs. Namespace/Database isolation

Plan's example uses shared table with `tenant` column checked in `WHERE`-style clause. Fails open silently: one missed clause, one typo, or one new table someone forgets to annotate, and it's cross-tenant data leak — actual bug class behind most real-world RLS incidents in Supabase-style stacks. SurrealDB's native Namespace/Database-per-tenant isolation (available since 3.0) is structural instead: buggy permission clause inside tenant A's namespace still cannot reach tenant B's data because there's no shared table to query. Recommend evaluating NS/DB-per-tenant isolation regardless of 6.2 decision.

---

### 6.4 JWT revocation has no built-in answer

SurrealDB doesn't maintain a revocation list — checks signature and expiry only (plus JWKS-cached keys, refreshed on ~12h cycle). No native instant-revoke primitive. Realistic mitigation is conventional one: short-lived access tokens (minutes) minted by .NET, paired with server-side refresh tokens .NET can invalidate immediately. Logout, role changes, and tenant deactivation take effect on next refresh, not instantly — inherent latency window team should accept explicitly, or close via SurrealDB `AUTHENTICATE` clause re-checking a live "active" flag on every request.
