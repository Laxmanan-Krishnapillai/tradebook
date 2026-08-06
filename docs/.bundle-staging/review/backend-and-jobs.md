# Backend & Background Jobs

*Part of the [architecture review](README.md).*

### 6.5 Hangfire requires a second datastore

Confirmed: no SurrealDB storage provider for Hangfire, official or community. Hangfire's supported backends: SQL Server, PostgreSQL, Redis, MongoDB; "Memory" option single-process and non-persistent (jobs vanish on restart, no multi-instance coordination) — not viable for real background-job needs. Adopting Hangfire as planned means standing up **second datastore** purely for job storage. Of candidates already listed (Postgres/Redis/Memory), Postgres most natural choice if persistence + multi-instance safety matter — but call out this dependency explicitly in Section 2B as infrastructure, not leave implicit in a parenthetical.
