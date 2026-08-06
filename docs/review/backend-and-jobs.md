# Backend & Background Jobs

*Part of the [architecture review](README.md).*

### 6.5 Hangfire requires a second datastore

Confirmed: there is no SurrealDB storage provider for Hangfire, official or community. Hangfire's supported backends are SQL Server, PostgreSQL, Redis, and MongoDB; the "Memory" option is single-process and non-persistent (jobs vanish on restart, no multi-instance coordination), so it isn't viable for real background-job needs. Adopting Hangfire as currently planned means standing up a **second datastore** purely for job storage. Of the candidates already listed (Postgres/Redis/Memory), Postgres is the most natural choice if persistence and multi-instance safety matter — but this dependency should be called out explicitly in Section 2B as infrastructure, not left implicit in a parenthetical.
