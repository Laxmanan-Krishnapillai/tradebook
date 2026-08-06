# CRDT-Based Canvas Collaboration - Yjs / Automerge / Liveblocks / PartyKit

*Part of [Alternative Architecture Exploration](README.md).*

### 7.3 CRDT-based canvas collaboration — Yjs / Automerge / Liveblocks / PartyKit (corrected assumption, not recommendation)

Important finding: CRDTs-for-canvas-tools largely myth as commonly assumed. tldraw's own engineering blog explicitly states it does not use Yjs for core sync; Figma's engineering blog says they deliberately avoid "true CRDTs." Both use server-authoritative last-writer-wins reconciliation instead. CRDTs solve *multi-writer* conflict merging without server round-trips — with one editor per canvas at a time (this plan's workflow-canvas feature appears to be, per Section 3's folder structure), that problem doesn't exist yet. **Recommendation: use local component state plus debounced writes to SurrealDB (or Postgres, per §7.2) now; don't build CRDT infrastructure preemptively.** If real concurrent multiplayer editing becomes committed roadmap item later, add Yjs via PartyKit (self-hostable on Cloudflare) as ephemeral session layer that flushes into durable store — not as replacement for it.
