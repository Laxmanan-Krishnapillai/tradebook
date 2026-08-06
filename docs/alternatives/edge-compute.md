# Edge Compute - Cloudflare Workers + Durable Objects

*Part of [Alternative Architecture Exploration](README.md).*

### 7.5 Edge compute — Cloudflare Workers + Durable Objects

Durable Object SQLite storage is now GA with near-zero cold starts, and one-Durable-Object-per-canvas-document is a real, documented pattern for presence/live-cursor fan-out. But the latency that matters most for this product is client-side rendering (§6.10), not server round-trips — if the user base clusters in a few regions (typical for a B2B dashboard), a well-placed regional `.NET` deployment already captures most of that budget. The real value here is narrow: collaborative presence and session coordination alongside the existing backend, not a replacement for it. **Skip this unless the user base is globally dispersed with many concurrent users on the same shared document** — likely not the case here.
