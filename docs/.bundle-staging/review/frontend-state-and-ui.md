# Frontend State, UI & the Read/Write Split

*Part of the [architecture review](README.md).*

### 6.6 Frontend gap: TanStack Router and Virtual are adopted, TanStack Query is not

App also talks to a separate .NET REST API for "complex operations," but no server-state/caching library listed for that leg. Without one, teams typically fall back to `useEffect` + `fetch`, hand-rolled hooks, or async Zustand actions stashing `loading/error/data` in the store — reinventing deduplication, retry/backoff, and race-condition handling per feature. Given app already juggles XState and SurrealDB live-query state, adding a fourth ad-hoc data pattern for REST is the wrong call. **Recommendation**: add `@tanstack/react-query` for .NET REST leg now, not later.

---

### 6.7 Aceternity UI / Animate UI vs. the "ultra-fast client-side execution" goal

Real tension, not overblown — but it's about placement, not a reason to drop them. These are copy-paste Framer Motion collections; glow/beam/hero effects lean on `box-shadow`, `filter`/`backdrop-filter` blur, and continuously-running transforms, which are paint/composite-heavy unless deliberately scoped via `LazyMotion` + `m` (full Framer Motion is ~34kb gzip; the lazy path is ~4.6kb, and teams routinely skip the setup that gets you there). In marketing pages, empty states, and onboarding, cost is negligible. Inside or near the canvas/editor — where React Flow and TanStack Virtual are already competing for frame budget — it directly undercuts the stated performance goal. **Recommendation**: confine these effects to non-interactive surfaces; keep them out of canvas/editor views.

---

### 6.8 React Flow + dnd-kit: specific, documented friction (not just generic "pointer conflicts")

Two concrete, documented failure modes beyond what Section 2's "Integration Rule" acknowledges:
- React Flow pans/zooms via a CSS `transform: scale()` on its viewport pane, but dnd-kit's positioning/collision detection uses `getBoundingClientRect` in screen pixels — at any zoom level other than 100%, the drag ghost and drop targets visibly desync ([xyflow discussion #4354](https://github.com/xyflow/xyflow/discussions/4354)).
- React Flow's own node-drag handling and dnd-kit's `PointerSensor` both capture `pointerdown` on the same nodes, so sortable lists nested inside custom nodes need React Flow's drag disabled (`nodrag` class / `draggable={false}`) and usually a per-node-scoped `DndContext` rather than one canvas-wide context ([xyflow/xyflow#3768](https://github.com/xyflow/xyflow/issues/3768), [react-flow#1765](https://github.com/wbkd/react-flow/issues/1765)).

Budget real integration time for zoom-aware coordinate translation and scoped drag contexts; this is not a drop-in combination.

---

### 6.11 Frontend implementation pattern for the read/write split

§6.2 resolves the client/backend split as: reads and live queries go direct to SurrealDB (read-only), all writes go through .NET. This section answers the follow-up question — how that split is implemented on the frontend without creating a visible seam between the two paths.

**Read side: `surqlize`.** [`surrealdb/surqlize`](https://github.com/surrealdb/surqlize) is an officially-maintained (SurrealDB org) type-safe TypeScript query builder/ORM for the JS SDK (2.0+), with full CRUD, graph edges, and live-query subscriptions (`db.live()` compiling to `LIVE SELECT`, supporting filters, projections, and JSON Patch diffs rather than full-record snapshots — good for the §6.10 low-memory goal). Use it exclusively for the read path. Because the client's SurrealDB connection is permissioned read-only, any accidental `db.create()`/`db.update()` call through it is rejected at the database layer — the CQRS boundary is enforced structurally, not just by convention.

**The reconciliation problem.** Naively, reads (live-query push) and writes (`.NET` mutation) are two independent paths with no shared state, so a write feels laggy (wait for `.NET` round trip → SurrealDB commit → live-query push back) unless optimistic UI is layered on, and optimistic UI creates its own problem: the optimistic entry and the eventual live-query confirmation need to land as the *same* entry, or the UI flickers/duplicates.

**The fix: one canonical cache, one ID, used throughout.**
- Use **TanStack Query as the canonical entity cache**, even though its data originates from a live push rather than always from a fetch: seed each query key with an initial typed `SELECT` as `queryFn`, then feed subsequent `db.live()` events into the same key via `queryClient.setQueryData`.
- For **creates**, generate a stable client-side ID (ULID/UUID) at the moment of the user's action and send it to `.NET`, which creates the SurrealDB record with that exact ID rather than letting SurrealDB auto-generate one (SurrealDb.Net: set `Id = ("table", clientId)` on the object passed to `db.Create()` — [docs](https://surrealdb.com/docs/sdk/dotnet/methods/create)). The optimistic cache write, the mutation, and the eventual live-query push all key off the same ID — no temp-ID swap step, no duplicate-entry window. Updates/deletes don't have this problem since the ID already exists.
- `onMutate` writes optimistically into that same query key (and any relevant list-view key); `onError` rolls back via the standard TanStack Query snapshot pattern. No explicit `onSuccess` cache write is needed for the happy path — the live-query push arrives shortly after and overwrites the optimistic entry with the confirmed one, which is normally invisible since the content matches.

**Race condition most naive versions of this pattern miss** (confirmed by checking a published TanStack-Query-plus-WebSocket writeup, which does not handle it): if a live-query event arrives while the seeding `SELECT` is still in flight, and the `SELECT` resolves afterward, it silently overwrites newer pushed data with stale data. Guard every merge with a version/`updatedAt` comparison (`if (incoming.updatedAt <= cached.updatedAt) skip`), or buffer live events until the initial load resolves and replay them in order.

**Sketch:**
```ts
function useProject(id: string) {
  const queryClient = useQueryClient()
  const queryKey = ['project', id]

  const query = useQuery({
    queryKey,
    queryFn: () => db.select('project').where({ id }).one(),
  })

  useEffect(() => {
    const sub = db.live('project', { id }, (event) => {
      queryClient.setQueryData(queryKey, (old) =>
        old && event.updatedAt <= old.updatedAt ? old : mergePatch(old, event)
      )
    })
    return () => sub.unsubscribe()
  }, [id])

  return query
}

function useUpdateProject() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (patch) => api.patch(`/projects/${patch.id}`, patch), // .NET FastEndpoints
    onMutate: async (patch) => {
      const key = ['project', patch.id]
      const previous = queryClient.getQueryData(key)
      queryClient.setQueryData(key, (old) => ({ ...old, ...patch, updatedAt: Date.now() }))
      return { previous }
    },
    onError: (_err, patch, ctx) => queryClient.setQueryData(['project', patch.id], ctx.previous),
  })
}

function useCreateProject(tenantId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (draft) => api.post('/projects', draft), // draft.id is client-generated (ulid())
    onMutate: async (draft) => {
      queryClient.setQueryData(['project', draft.id], { ...draft, _optimistic: true })
      queryClient.setQueryData(['projects', tenantId], (list) => [...(list ?? []), draft])
    },
  })
}
```

This same pattern is what makes drag-and-drop reordering (dnd-kit, §6.8) and canvas node edits (React Flow) feel instant: the reorder/edit writes optimistically into the shared cache immediately, and the `.NET` write + SurrealDB live-query confirmation reconcile silently in the background.

**Upgrade path worth piloting**: everything above is a hand-rolled version of what TanStack DB provides natively (incremental live-query joins, built-in optimistic mutations with the same shared-ID reconciliation), and a community collection adapter already wraps SurrealDB's `LIVE` stream directly with zero backend changes required — see §7.8 for the full analysis and why it's recommended as a low-risk pilot (kanban board first) rather than an app-wide rewrite.

*Sources: [surqlize](https://github.com/surrealdb/surqlize), [SurrealDb.Net Create method (explicit record ID)](https://surrealdb.com/docs/sdk/dotnet/methods/create), [TanStack Query optimistic updates guide](https://tanstack.com/query/latest/docs/framework/react/guides/optimistic-updates)*
