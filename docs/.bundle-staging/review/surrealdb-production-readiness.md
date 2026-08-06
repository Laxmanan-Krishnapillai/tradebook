# SurrealDB Production Maturity & Licensing

*Part of the [architecture review](README.md).*

### 6.9 SurrealDB production maturity & licensing

Public evidence of SurrealDB in production exists but is almost entirely vendor-published (SurrealDB's own case-studies page: Samsung, Tencent, Verizon, Saks Fifth Avenue, PolyAI, Aspire Comps, and others). No independent "we ran it at scale, here's what broke" retrospectives were found, but also no publicized migrations away from it. Independently verifiable concerns, from SurrealDB's own GitHub issue tracker and advisories:

- **Stability**: reports of spontaneous shutdowns, crashes on large-offset queries over large tables, and memory leaks/OOM during bulk restore.
- **Backup/restore immaturity**: the only backup format is SQL-text (`.surql`); restoring 200k records across 69 tables took over 7 hours in one reported case because restore replays statements sequentially rather than using binary snapshots. Binary backups are still a planned future feature.
- **Breaking changes every major version**: 1.x→2.x removed `DEFINE SCOPE` and changed `UPDATE`/`UPSERT` semantics; 2.x→3.x removed the `<future>` type; on-disk format changes require running `surreal fix`.
- **Security churn**: roughly 10 advisories published May–July 2026 alone, several being permission-bypass bugs in the PERMISSIONS/field-level-security system — notable given row/field permissions are the exact mechanism Section 2 relies on for tenant isolation.
- **Clustering complexity**: horizontal scaling relies on TiKV + PD underneath stateless SurrealDB nodes (a ~3+3 node minimum topology), adding real operational overhead.
- **Ecosystem/hiring**: minimal StackOverflow presence, thin embedding docs for some SDK languages.

**Licensing**: core SurrealDB is Business Source License 1.1, not OSI-open-source. Embedding it commercially in a product/SaaS is unrestricted and fee-free; the only prohibited use is reselling SurrealDB itself as a managed database service to third parties without a commercial license. Each release converts to Apache 2.0 four years later. This is a non-issue for an ordinary product build — it only matters if this project were itself a DBaaS.

**Read**: SurrealDB is genuinely running in production for consolidation-style and AI/agent-memory workloads, but the evidence base is vendor-curated rather than independently corroborated, and it still ships frequent breaking changes and permission-related CVEs in its 3.x line. Reasonable for a greenfield app tolerant of some churn; warrants real due diligence (backup/restore drills, patch cadence, clustering ops burden) before treating it as the system of record for anything mission-critical.

*Sources: [SurrealDB case studies](https://surrealdb.com/casestudies), [Aspire Comps case study](https://dev.to/surrealdb/how-aspire-comps-replaced-5-backend-tools-with-surrealdb-and-scaled-to-700000-users-2ddf), [independent comparative analysis](https://caperaven.co.za/2025/04/01/surrealdb-in-2025-a-comparative-analysis-across-database-categories-briefing-document/), [SurrealDB license FAQ](https://surrealdb.com/license), [Surreal Cloud launch coverage](https://siliconangle.com/2024/12/04/surrealdbs-surreal-cloud-debuts-aws-s3-scalable-multi-model-dbaas/)*
