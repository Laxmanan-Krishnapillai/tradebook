# Edge Compute - Cloudflare Workers + Durable Objects

*Part of [Alternative Architecture Exploration](README.md).*

### 7.5 Edge compute — Cloudflare Workers + Durable Objects

Durable Object SQLite storage now GA with near-zero cold starts, one-Durable-Object-per-canvas-document real, documented pattern for presence/live-cursor fan-out. But latency mattering most for this product is client-side rendering (§6.10), not server round-trips — if user base clusters in few regions (typical for B2B dashboard), well-placed regional `.NET` deployment already captures most of that budget. Real value here narrow: collaborative presence and session coordination alongside existing backend, not replacement for it. **Skip this unless user base globally dispersed with many concurrent users on same shared document** — likely not case here.
