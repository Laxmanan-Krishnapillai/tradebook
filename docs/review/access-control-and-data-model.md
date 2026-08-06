# Access Control & Data Model

*Part of the [architecture review](README.md).*

### 6.1 Correctness bug: the RLS/JWT example as written will not work

Section 2's permission example reads `$auth.tenant_id` and `$auth.role`. That only populates if the frontend authenticates via `DEFINE ACCESS ... TYPE RECORD` (SurrealDB looks up a real database user record and fills `$auth` from its fields). Authenticating with a raw, externally-issued JWT via `DEFINE ACCESS ... TYPE JWT` — i.e. "hand SurrealDB the same JWT .NET issued," as Section 2 currently describes — populates `$token.*`, not `$auth.*`, and doesn't require or consume an `id` claim at all.

One of two things has to change before this ships:
- Rewrite every permission clause to reference `$token.tenant_id` / `$token.role`, or
- Adopt `TYPE RECORD ... WITH JWT`, which requires a real SurrealDB user record per person, with `tenant_id`/`role` fields kept in sync with whatever .NET treats as the source of truth for those attributes.

**This is not a stylistic choice — `TYPE RECORD` is mandatory if any per-tenant/per-role restriction on client connections matters at all.** A plain `TYPE JWT` access method authenticates as a system-level user, which SurrealDB's own docs describe as having access "equivalent to system users, which is above fine-grained permissions" — i.e. it **bypasses table `PERMISSIONS` clauses entirely**, regardless of what's written in them. If §6.2's read-only-direct pattern below is adopted, the client-facing access method must be `TYPE RECORD`; a generic externally-issued JWT cannot be scoped down to read-only via `PERMISSIONS` no matter how the clauses are written.

Also unresolved: the JWT key configuration. HS256 with a shared secret is simplest (.NET and SurrealDB hold the same secret) but means that secret must rotate in lockstep across two systems. RS256/JWKS lets SurrealDB fetch .NET's public keys and rotate without redeploying SurrealDB config, at the cost of standing up and maintaining a JWKS endpoint. Pick one explicitly.

*Source: [DEFINE ACCESS TYPE JWT](https://surrealdb.com/docs/surrealql/statements/define/access/jwt), [DEFINE ACCESS TYPE RECORD](https://surrealdb.com/docs/surrealql/statements/define/access/record)*

---

### 6.2 The central risk: direct client-to-SurrealDB access over the public internet

Section 1's architecture has the browser connect straight to SurrealDB over `ws://`/`wss://` for "simple" CRUD and live queries, with SurrealDB's PERMISSIONS clauses as the *only* authorization boundary on that path. This is architecturally similar to Supabase/Firebase, but neither of those actually skips a gateway the way this plan does:

- **Firebase** ships a dedicated emulator and `@firebase/rules-unit-testing` framework for unit-testing security rules pre-deploy, plus Cloud Audit Logging of every rule evaluation and App Check.
- **Supabase** exposes Postgres through PostgREST — a generated REST layer, not a raw wire-protocol connection — sitting in front of RLS.
- **Hasura** generates a typed GraphQL API and supports an allow-list of pre-approved operations per role. It is a BFF by design.
- **PowerSync / ElectricSQL** interpose a sync/replication layer: reads are scoped by declarative sync rules, writes go through an upload queue the backend still validates. The client never gets a raw connection to the database.

SurrealDB gives you `PERMISSIONS` and `DEFINE EVENT` and expects the team to build the testing, audit logging, and rate limiting itself. SurrealDB's own security docs recommend it be exposed exclusively to the internal network, not the public internet — which a browser-facing WebSocket endpoint necessarily violates.

**Blast radius under XSS**: with fixed backend endpoints, a compromised client can only replay a finite, already-validated request set. With raw query access, it inherits the entire SurrealQL surface — it can construct novel queries and probe every table the JWT's role could theoretically touch. The attack surface becomes "everything SurrealQL can express," not "the endpoints we wrote."

**Dual write path**: `DEFINE EVENT` can replicate side effects (audit trails, notifications, search-index updates) for direct writes, but events skip permission checks and `ASYNC` events run on a polling interval (5s default), not instantly. Side effects now live in two places — backend code for the "complex" path, DB events for the "simple" path — and drift silently the first time someone adds a side effect to one and forgets the other.

**Original recommendation (superseded below)**: blanket-proxy everything through the .NET backend. After the team clarified the priority is a low-latency, buttery-smooth, low-memory dashboard, a follow-up research pass (§6.11) found a more precise middle ground that keeps the performance properties direct access is meant to buy, without the open-ended write exposure.

**Resolved recommendation: read-only direct access, write-only via backend.**
- The browser's direct SurrealDB connection is authorized via `TYPE RECORD` access and is granted **SELECT and LIVE SELECT only** — every client-facing table defaults to `PERMISSIONS NONE` and explicitly allowlists `select`:
  ```surrealql
  DEFINE TABLE project SCHEMAFULL
      PERMISSIONS
          FOR select WHERE tenant = $auth.tenant_id AND (owner = $auth.id OR $auth.role = 'admin')
          FOR create, update, delete NONE;
  ```
- **All writes** go exclusively through the .NET backend, which holds its own privileged connection. SurrealDB's change-feed mechanism then fans the resulting update out to subscribed browsers automatically — the backend does not need to implement its own pub/sub relay for this to work.
- This closes the §6.2 write-path attack surface (no data corruption, no injected records, no privilege escalation via a client-crafted write) and the "dual write path" business-logic problem (there's exactly one route into the data — audit trails and side effects live in one place), while leaving live-query push latency untouched, because reads never leave the direct browser↔SurrealDB path.
- This has real precedent: it's the same shape as Supabase (anon/authenticated key + RLS for direct reads/Realtime, `service_role` reserved for privileged backend writes) and Hasura (`backend_only` mutations/Actions for writes, direct queries/subscriptions for reads).
- **Residual risk, do not skip**: SurrealDB's historical default was `PERMISSIONS FULL` on any table without an explicit clause (CVSS 8.8, fixed in 1.0.1) — one table without an explicit `NONE` reopens full write access. Recent CVEs (CVE-2025-11060, GHSA-hv6h-hc26-q48p) show `LIVE SELECT` can leak data because permissions are evaluated at subscribe-time, not at notification-time, and via graph traversals materializing related records that bypass field-level filters. Mitigation: default every table to `PERMISSIONS NONE` explicitly, pin a patched SurrealDB version, and add a CI check that fails the build if any client-facing table grants `create`/`update`/`delete`.
- If the team decides the residual risk above is still unacceptable, full backend-proxying remains the fallback — §6.11 found that costs low single-digit milliseconds in a same-region deployment, imperceptible against the dashboard's stated performance goals. So this is not a performance-forced choice either way; it's a risk-tolerance choice with a clear low-cost fallback.

*Sources: [SurrealDB security best practices](https://surrealdb.com/docs/learn/security/best-practices/security-best-practices), [SurrealDB security troubleshooting](https://surrealdb.com/docs/surrealdb/security/troubleshooting), [DEFINE ACCESS record](https://surrealdb.com/docs/surrealql/statements/define/access/record), [Permissions & RLS](https://surrealdb.com/docs/learn/security/authorization/permissions-and-row-level-security), [Default FULL permissions advisory](https://github.com/surrealdb/surrealdb/security/advisories/GHSA-x5fr-7hhj-34j3), [LIVE query exposure CVE-2025-11060](https://advisories.gitlab.com/cargo/surrealdb/CVE-2025-11060/), [Supabase RLS docs](https://supabase.com/docs/guides/database/postgres/row-level-security), [Hasura backend-only mutations/Actions](https://hasura.io/docs/2.0/actions/derive/), [Hasura allow-list](https://hasura.io/docs/2.0/security/allow-list/)*

---

### 6.3 Multi-tenancy: row-level tenant column vs. Namespace/Database isolation

The plan's example uses a shared table with a `tenant` column checked in a `WHERE`-style clause. That fails open silently: one missed clause, one typo, or one new table someone forgets to annotate, and it's a cross-tenant data leak — this is the actual bug class behind most real-world RLS incidents in Supabase-style stacks. SurrealDB's native Namespace/Database-per-tenant isolation (available since 3.0) is structural instead: a buggy permission clause inside tenant A's namespace still cannot reach tenant B's data because there is no shared table to query. Recommend evaluating NS/DB-per-tenant isolation regardless of the 6.2 decision.

---

### 6.4 JWT revocation has no built-in answer

SurrealDB does not maintain a revocation list — it checks signature and expiry only (plus JWKS-cached keys, refreshed on a ~12h cycle). There is no native instant-revoke primitive. The realistic mitigation is the conventional one: short-lived access tokens (minutes) minted by .NET, paired with server-side refresh tokens .NET can invalidate immediately. Logout, role changes, and tenant deactivation take effect on next refresh, not instantly — an inherent latency window the team should accept explicitly, or close via a SurrealDB `AUTHENTICATE` clause that re-checks a live "active" flag on every request.
