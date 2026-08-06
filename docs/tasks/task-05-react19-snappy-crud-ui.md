# Task 05: React 19 Local-First Optimistic Snappy CRUD UI & TanStack DB Integration

> **DESCOPE NOTICE (2026-08-06 — applied in this revision)** — per [`architecture/decision-log.md`](../architecture/decision-log.md) **D5**: offline-first is out of scope. The Dexie.js mutation queue, compaction, `/api/v1/mutations/batch` sync, and `perform3WayMerge` (zero call sites) are removed. Replacement: TanStack Query per-mutation optimistic updates + rollback; concurrency via the server `version` column — on 409, refetch and show a conflict prompt (never silent client-wins). Undo/redo is an in-memory, session-scoped command stack. The duplicate stub file `task-05-react-snappy-crud-ui.md` has been deleted; this file is the only Task 05 spec. The H1 keeps its historical name for filename stability; the body lists the real dependency set.

- **Phase**: Frontend UI & User Experience
- **Lead / Owner**: Frontend Lead Architect
- **Complexity**: High
- **Prerequisites**: Task 02 (.NET 9 Backend Core), Task 03 (SignalR Real-Time Engine), Task 08 (Agent-Readiness & TypeGen Contracts)
- **Target Files**:
  - `src/Frontend/package.json`
  - `src/Frontend/vite.config.ts`
  - `src/Frontend/src/lib/api/client.ts`
  - `src/Frontend/src/lib/mutations/entityMutations.ts`
  - `src/Frontend/src/lib/commands/UndoRedoStack.ts`
  - `src/Frontend/src/lib/realtime/signalRClient.ts`
  - `src/Frontend/src/lib/streaming/eventBatcher.ts`
  - `src/Frontend/src/components/canvas/ZoomAwareDndContext.tsx`
  - `src/Frontend/src/components/ui/CommandPalette.tsx`
  - `src/Frontend/src/components/ui/ConflictDialog.tsx`
  - `src/Frontend/src/lib/state/stateBoundaries.ts`
  - `src/Frontend/src/components/grid/VirtualizedDataTable.tsx`
  - `src/Frontend/tests/mutations/entityMutations.test.ts`
  - `src/Frontend/tests/commands/UndoRedoStack.test.ts`
  - `src/Frontend/tests/streaming/eventBatcher.test.ts`
  - `src/Frontend/tests/realtime/signalRCatchUp.test.ts`
  - `src/Frontend/tests/canvas/ZoomAwareDndContext.test.tsx`

---

## Executive Overview

Task 05 defines the architecture and implementation blueprint for Tradebook's snappy, keyboard-first React 19 CRUD UI, comparable to Linear and Twenty CRM. Edits render instantly from the TanStack Query cache via per-mutation optimistic updates; the server remains the source of truth through optimistic concurrency control (OCC) on a `version BIGINT` column, with every conflict surfaced to the user — never silently merged.

Core mechanisms: TanStack Query `useMutation` with rollback snapshots, standard REST endpoints carrying the entity `version`, an HTTP 409 conflict dialog flow, an in-memory session-scoped `UndoRedoStack` that replays inverse mutations through the same endpoints, the Task 03 SignalR `EntityChanged` stream with reconnect catch-up and eventId dedup, an RxJS `bufferTime(50)` coalescer for price-update bursts, a scale-synchronized drag-and-drop canvas (`ZoomAwareDndContext`), and a cmdk global command palette.

```
+------------------------------------------------------------------------------+
| React 19 SPA                                                                 |
|   Zustand (UI state) | XState (FSMs) | TanStack Query v5 (server cache)      |
+------------------------------------------------------------------------------+
      | useMutation: onMutate -> rollback snapshot + optimistic cache update
      v
  REST endpoints (e.g. PUT /api/v1/deliveries/{id}, body carries `version`)
      |   200 -> invalidate & reconcile
      |   409 -> roll back cache, refetch, ConflictDialog (never client-wins)
      |   network error -> TanStack default retry (3, exponential), then toast
      v
  .NET 9 API + PostgreSQL (UPDATE ... WHERE id = $1 AND version = $2)
      |
      |  outbox -> SignalR /hubs/dashboard (MessagePack, JWT)
      v
  EntityChanged stream -> RxJS bufferTime(50) coalescer -> cache patches
```

---

## 1. Objectives, Scope, Dependencies & Prerequisites

### 1.1 Core Objectives
1. **Optimistic per-mutation updates**: every entity mutation applies to the TanStack Query cache in `onMutate` (after capturing a rollback snapshot), so the UI reflects the edit immediately, before the network round-trip completes.
2. **Optimistic concurrency, surfaced conflicts**: every mutating request carries the entity's `version` (BIGINT). On HTTP 409 the client rolls back the optimistic update, refetches server truth, and shows a conflict dialog. **Silent client-wins is forbidden (D5).**
3. **Honest error handling**: network errors use TanStack Query's default retry (3 attempts, exponential backoff); after exhaustion the cache is rolled back and an error toast is surfaced. HTTP 4xx responses are not retried.
4. **Undo/redo**: an in-memory, session-scoped command stack (`Cmd+Z` / `Cmd+Shift+Z`) that replays inverse mutations through the same REST endpoints — subject to the same OCC rules.
5. **Live updates**: consume the Task 03 SignalR `EntityChanged` stream with reconnect catch-up and eventId dedup, patching the query cache.
6. **Event batching**: an RxJS `bufferTime(50)` window coalesces price-update bursts from the `EntityChanged` stream so the main thread renders once per window per aggregate.
7. **Scale-synchronized canvas DnD**: `ZoomAwareDndContext` keeps dragged React Flow nodes aligned under the cursor at any zoom level.
8. **Keyboard-first ergonomics**: a global cmdk command palette with navigation shortcuts (`g d`, `g c`, ...) and fuzzy search.

### 1.2 Prerequisites & Dependencies
- **Task 02**: entity CRUD REST endpoints with `version`-based OCC (409 on version mismatch).
- **Task 03**: SignalR hub `/hubs/dashboard` + `GET /api/v1/events` catch-up endpoint.
- **Task 08**: TypeGen DTOs in `src/Frontend/src/types/generated/`.
- **Frontend packages** (real, pinned major versions — note: `@tanstack/react-query` + `@tanstack/react-table` are the actual TanStack packages used; the H1's "TanStack DB" is historical naming only):
  - `react@^19.0.0`, `react-dom@^19.0.0`
  - `@tanstack/react-query@^5.0.0`, `@tanstack/react-table@^8.0.0`
  - `@microsoft/signalr@^8.0.0`, `@microsoft/signalr-protocol-msgpack@^8.0.0`
  - `rxjs@^7.8.0`
  - `@xyflow/react@^12.0.0` (React Flow)
  - `@dnd-kit/core@^6.1.0`, `@dnd-kit/modifiers@^7.0.0`
  - `cmdk@^1.0.0` (maintained command palette; replaces the previous beta-status palette dependency)
  - `zustand@^4.5.0`, `xstate@^5.10.0`

---

## 2. Architecture & State Boundaries Matrix

### 2.1 State Boundary Allocation

| State Category | Responsible Library | Storage Location | Lifetime & Scope | Example Usage |
| :--- | :--- | :--- | :--- | :--- |
| **Server Entity Cache** | TanStack Query v5 | In-memory QueryCache | Session / server-synced | Physical deliveries, contracts, capacity bookings, market prices |
| **Global Ephemeral UI** | Zustand | Memory (`useUIStore`) | Session | Sidebar collapse, active modal, focused cell, dark mode |
| **Auth** | Zustand (`useAuthStore`) | Memory | Session | JWT access token consumed by `apiFetch` and SignalR |
| **Workflow State Machine** | XState v5 | Memory (`useActor`) | Canvas component lifetime | Multi-step canvas node creation FSM |
| **Command History** | `UndoRedoStack` | Memory | Session | Undo/redo command buffer |
| **Realtime Stream Cursor** | `signalRClient` | Memory only — not persisted | Session | Highest seen `sequenceId` + LRU set of last 10,000 `eventId`s |

There is no persistent client-side storage tier: no durable mutation log, no offline queue (D5).

### 2.2 React 19 CSR SPA Layout

```
src/Frontend/
├── src/
│   ├── components/
│   │   ├── canvas/        (WorkflowCanvas, ZoomAwareDndContext, ZoomAwareDragOverlay)
│   │   ├── grid/          (VirtualizedDataTable, GridCellEditors, GridToolbar)
│   │   ├── layout/        (AppHeader, AppLayout, Sidebar)
│   │   └── ui/            (CommandPalette, ConflictDialog, StatusBadge)
│   ├── hooks/             (useCommandStack, useSignalRStream, useVirtualizedTable)
│   ├── lib/
│   │   ├── api/client.ts
│   │   ├── commands/      (Command.ts, UndoRedoStack.ts)
│   │   ├── mutations/entityMutations.ts
│   │   ├── realtime/signalRClient.ts
│   │   ├── state/         (stateBoundaries.ts, useUIStore.ts, useAuthStore.ts, workflowFSM.ts)
│   │   └── streaming/eventBatcher.ts
│   ├── types/generated/   <-- TypeGen output (Task 08)
│   ├── App.tsx
│   └── main.tsx
├── tests/                 <-- test root: src/Frontend/tests/
│   ├── mutations/entityMutations.test.ts
│   ├── commands/UndoRedoStack.test.ts
│   ├── streaming/eventBatcher.test.ts
│   ├── realtime/signalRCatchUp.test.ts
│   └── canvas/ZoomAwareDndContext.test.tsx
└── vite.config.ts
```

---

## 3. Implementation Code Contracts & Blueprints

### 3.1 API Client Wrapper (`src/Frontend/src/lib/api/client.ts`)

Every HTTP call — queries, mutations, catch-up paging — goes through this wrapper so `Authorization: Bearer` from the auth store is attached uniformly. SignalR uses the same store via `accessTokenFactory` (§3.4).

```typescript
import { useAuthStore } from '../state/useAuthStore';

export class ApiError extends Error {
  constructor(public status: number, public problem?: unknown) {
    super(`HTTP ${status}`);
  }
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = useAuthStore.getState().accessToken;
  const response = await fetch(path, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      ...init.headers
    }
  });
  if (!response.ok) {
    let problem: unknown;
    try { problem = await response.json(); } catch { /* no body */ }
    throw new ApiError(response.status, problem);
  }
  return response.status === 204 ? (undefined as T) : response.json();
}
```

### 3.2 Optimistic Mutations & 409 Conflict Flow (`src/Frontend/src/lib/mutations/entityMutations.ts`)

One `useMutation` hook per entity (deliveries shown; contracts, capacity bookings, transfers follow the identical pattern). Mutations call the **standard REST endpoints** and carry the entity's `version`.

```typescript
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiFetch, ApiError } from '../api/client';
import type { PhysicalDeliveryDto, UpdatePhysicalDeliveryDto } from '../../types/generated';

export interface UpdateDeliveryVariables {
  id: string;
  version: number; // BIGINT version of the row being edited
  changes: UpdatePhysicalDeliveryDto;
}

export function useUpdateDelivery(onConflict: (id: string) => void) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, version, changes }: UpdateDeliveryVariables) =>
      apiFetch<PhysicalDeliveryDto>(`/api/v1/deliveries/${id}`, {
        method: 'PUT',
        body: JSON.stringify({ ...changes, version })
      }),

    // TanStack Query default retry semantics: up to 3 attempts with exponential
    // backoff for network errors; HTTP responses (ApiError) are never retried.
    retry: (failureCount, error) => !(error instanceof ApiError) && failureCount < 3,

    onMutate: async ({ id, changes }) => {
      await queryClient.cancelQueries({ queryKey: ['deliveries'] });
      const snapshot = queryClient.getQueryData<PhysicalDeliveryDto[]>(['deliveries']);
      queryClient.setQueryData<PhysicalDeliveryDto[]>(['deliveries'], (old = []) =>
        old.map((d) => (d.id === id ? { ...d, ...changes } : d))
      );
      return { snapshot };
    },

    onError: (error, { id }, context) => {
      // Roll back the optimistic update in EVERY error path.
      if (context?.snapshot) {
        queryClient.setQueryData(['deliveries'], context.snapshot);
      }
      if (error instanceof ApiError && error.status === 409) {
        // Version conflict: refetch server truth and surface the conflict
        // dialog. Silent client-wins is forbidden (D5).
        queryClient.invalidateQueries({ queryKey: ['deliveries'] });
        onConflict(id);
        return;
      }
      showErrorToast('Save failed', error);
    },

    onSettled: () => queryClient.invalidateQueries({ queryKey: ['deliveries'] })
  });
}
```

**`ConflictDialog.tsx`**: props `{ entityId, serverState, attemptedChanges, onClose }`. Shows the refetched server row next to the user's attempted edit; the user re-applies wanted fields manually (producing a fresh mutation with the new `version`). No automatic merging.

### 3.3 Command Pattern `UndoRedoStack` (`src/Frontend/src/lib/commands/UndoRedoStack.ts`)

In-memory, session-scoped. `undo()` replays the inverse mutation through the same REST endpoints — so it participates in OCC: if the entity changed since, the undo itself gets a 409 and the same conflict dialog; the stack entry is discarded. The stack is lost on refresh by design (D5).

```typescript
export interface Command {
  id: string;
  description: string;
  timestamp: number;
  execute(): Promise<void>; // forward mutation via the standard REST endpoint
  undo(): Promise<void>;    // inverse mutation via the same endpoint
}

export class UndoRedoStack {
  private undoStack: Command[] = [];
  private redoStack: Command[] = [];

  constructor(private maxSize = 100) {}

  public async pushAndExecute(command: Command): Promise<void> {
    await command.execute();
    this.undoStack.push(command);
    if (this.undoStack.length > this.maxSize) this.undoStack.shift();
    this.redoStack = []; // new command invalidates the redo branch
  }

  public async undo(): Promise<boolean> {
    const cmd = this.undoStack.pop();
    if (!cmd) return false;
    try {
      await cmd.undo();
      this.redoStack.push(cmd);
      return true;
    } catch {
      // 409 or network failure: the entity moved on — discard the entry;
      // the mutation layer has already surfaced the conflict dialog/toast.
      return false;
    }
  }

  public async redo(): Promise<boolean> {
    const cmd = this.redoStack.pop();
    if (!cmd) return false;
    await cmd.execute();
    this.undoStack.push(cmd);
    return true;
  }

  public canUndo(): boolean { return this.undoStack.length > 0; }
  public canRedo(): boolean { return this.redoStack.length > 0; }
}
```

### 3.4 SignalR Client (`src/Frontend/src/lib/realtime/signalRClient.ts`)

The client contract **must match Task 03 exactly**: typed event `EntityChanged(eventId: string, sequenceId: number, aggregateType: string, aggregateId: string, eventType: string, payloadJson: string)` on hub path `/hubs/dashboard`, MessagePack protocol, `accessTokenFactory` supplying the JWT, subscriptions via hub method `Subscribe("entity:PhysicalDelivery")` etc. Delivery is at-least-once → dedup by `eventId`.

```typescript
import * as signalR from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { apiFetch } from '../api/client';
import { useAuthStore } from '../state/useAuthStore';

export interface EntityChangedEvent {
  eventId: string;
  sequenceId: number;
  aggregateType: string;
  aggregateId: string;
  eventType: string;
  payloadJson: string;
}

const CATCH_UP_PAGE_SIZE = 500;
const DEDUP_LRU_CAPACITY = 10_000;

class LruSet {
  private map = new Map<string, true>();
  constructor(private capacity: number) {}
  has(key: string): boolean { return this.map.has(key); }
  add(key: string): void {
    if (this.map.has(key)) this.map.delete(key);
    this.map.set(key, true);
    if (this.map.size > this.capacity) {
      this.map.delete(this.map.keys().next().value!);
    }
  }
}

export class DashboardStreamClient {
  private connection: signalR.HubConnection;
  private lastSequenceId = 0;                              // memory only — never persisted
  private seenEventIds = new LruSet(DEDUP_LRU_CAPACITY);   // LRU of last 10,000 eventIds

  constructor(private onEvent: (e: EntityChangedEvent) => void) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/dashboard', {
        accessTokenFactory: () => useAuthStore.getState().accessToken
      })
      .withHubProtocol(new MessagePackHubProtocol())
      .withAutomaticReconnect()
      .build();

    this.connection.on(
      'EntityChanged',
      (eventId: string, sequenceId: number, aggregateType: string,
       aggregateId: string, eventType: string, payloadJson: string) =>
        this.handle({ eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson })
    );

    this.connection.onreconnected(() => void this.catchUp());
  }

  public async start(): Promise<void> {
    await this.connection.start();
    await this.connection.invoke('Subscribe', 'entity:PhysicalDelivery');
    await this.connection.invoke('Subscribe', 'entity:Contract');
    await this.connection.invoke('Subscribe', 'entity:MarketPrice');
    await this.catchUp();
  }

  private handle(e: EntityChangedEvent): void {
    if (this.seenEventIds.has(e.eventId)) return; // at-least-once delivery -> dedup
    this.seenEventIds.add(e.eventId);
    if (e.sequenceId > this.lastSequenceId) this.lastSequenceId = e.sequenceId;
    this.onEvent(e);
  }

  /** Reconnect catch-up: page GET /api/v1/events until a short page. */
  private async catchUp(): Promise<void> {
    let page: EntityChangedEvent[];
    do {
      page = await apiFetch<EntityChangedEvent[]>(
        `/api/v1/events?afterSequence=${this.lastSequenceId}&limit=${CATCH_UP_PAGE_SIZE}`
      );
      for (const e of page) this.handle(e);
    } while (page.length === CATCH_UP_PAGE_SIZE);
  }
}
```

### 3.5 RxJS `bufferTime(50)` Event Coalescer (`src/Frontend/src/lib/streaming/eventBatcher.ts`)

Applies to the Task 03 `EntityChanged` stream. During price-update bursts, only the highest-`sequenceId` event per aggregate inside each 50 ms window reaches the render path. Coalescing behavior is asserted in tests; there are no absolute throughput gates (D10).

```typescript
import { Subject, Subscription } from 'rxjs';
import { bufferTime, filter, map } from 'rxjs/operators';
import type { EntityChangedEvent } from '../realtime/signalRClient';

export class EntityEventBatcher {
  private inputStream$ = new Subject<EntityChangedEvent>();
  private subscription: Subscription | null = null;

  constructor(private windowTimeMs = 50) {}

  public start(onBatchReady: (batch: EntityChangedEvent[]) => void): void {
    this.subscription = this.inputStream$
      .pipe(
        bufferTime(this.windowTimeMs),
        filter((batch) => batch.length > 0),
        map((batch) => this.coalesceEventBatch(batch))
      )
      .subscribe(onBatchReady);
  }

  public pushEvent(event: EntityChangedEvent): void {
    this.inputStream$.next(event);
  }

  /** Within one window, only the highest-sequenceId event per aggregate survives. */
  public coalesceEventBatch(batch: EntityChangedEvent[]): EntityChangedEvent[] {
    const latest = new Map<string, EntityChangedEvent>();
    for (const event of batch) {
      const key = `${event.aggregateType}:${event.aggregateId}`;
      const existing = latest.get(key);
      if (!existing || event.sequenceId > existing.sequenceId) latest.set(key, event);
    }
    return Array.from(latest.values());
  }

  public stop(): void {
    this.subscription?.unsubscribe();
    this.subscription = null;
  }
}
```

### 3.6 React Flow + dnd-kit `ZoomAwareDndContext` (`src/Frontend/src/components/canvas/ZoomAwareDndContext.tsx`)

```tsx
import React, { ReactNode } from 'react';
import { DndContext, Modifier, DragOverlay } from '@dnd-kit/core';
import { useViewport } from '@xyflow/react';

interface ZoomAwareDndContextProps {
  children: ReactNode;
  onDragEnd: (event: any) => void;
}

/**
 * Scale-Sync Translator Modifier:
 * Scales raw screen delta vectors by 1 / zoom level to keep dragged node aligned under cursor.
 */
export function createZoomModifier(zoom: number): Modifier {
  return ({ transform }) => {
    if (!transform) return transform;
    return {
      ...transform,
      x: transform.x / zoom,
      y: transform.y / zoom
    };
  };
}

export const ZoomAwareDndContext: React.FC<ZoomAwareDndContextProps> = ({
  children,
  onDragEnd
}) => {
  const { zoom } = useViewport();
  const zoomModifier = createZoomModifier(zoom);

  return (
    <DndContext modifiers={[zoomModifier]} onDragEnd={onDragEnd}>
      {children}
    </DndContext>
  );
};

export const ZoomAwareDragOverlay: React.FC<{ children: ReactNode }> = ({ children }) => {
  const { zoom } = useViewport();

  return (
    <DragOverlay dropAnimation={null}>
      <div style={{ transform: `scale(${zoom})`, transformOrigin: 'top left' }}>
        {children}
      </div>
    </DragOverlay>
  );
};
```

### 3.7 cmdk Command Palette (`src/Frontend/src/components/ui/CommandPalette.tsx`)

```tsx
import React from 'react';
import { Command } from 'cmdk';
import { useNavigate } from 'react-router-dom';

export const CommandPalette: React.FC = () => {
  const [open, setOpen] = React.useState(false);
  const navigate = useNavigate();

  React.useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.key === 'k' && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        setOpen((o) => !o);
      }
    };
    document.addEventListener('keydown', down);
    return () => document.removeEventListener('keydown', down);
  }, []);

  const run = (fn: () => void) => { fn(); setOpen(false); };

  return (
    <Command.Dialog open={open} onOpenChange={setOpen} label="Command palette">
      <Command.Input placeholder="Type a command or search..." />
      <Command.List>
        <Command.Empty>No results.</Command.Empty>
        <Command.Group heading="Navigate">
          <Command.Item onSelect={() => run(() => navigate('/deliveries'))}>
            Go to Deliveries
          </Command.Item>
          <Command.Item onSelect={() => run(() => navigate('/contracts'))}>
            Go to Contracts
          </Command.Item>
          <Command.Item onSelect={() => run(() => navigate('/market-prices'))}>
            Go to Market Prices
          </Command.Item>
        </Command.Group>
        <Command.Group heading="Create">
          <Command.Item
            onSelect={() =>
              run(() => window.dispatchEvent(new CustomEvent('open-create-delivery-modal')))
            }
          >
            Create New Physical Delivery
          </Command.Item>
        </Command.Group>
      </Command.List>
    </Command.Dialog>
  );
};
```

Sequential navigation shortcuts (`g d`, `g c`, `g m`) are implemented by a small keydown-sequence listener in `AppLayout` dispatching the same navigation actions; cmdk provides the fuzzy-searchable dialog.

---

## 4. Subagent Implementation Step-by-Step Workflow

```
STEP 1: Vite + React 19 environment
  - package.json with the §1.2 dependency set (pinned major versions); vite.config.ts.

STEP 2: API client + auth wiring
  - lib/api/client.ts, useAuthStore; every fetch and hub connection carries the JWT.

STEP 3: Optimistic mutation hooks + conflict flow
  - lib/mutations/entityMutations.ts per entity; ConflictDialog.tsx; error toasts.

STEP 4: UndoRedoStack
  - lib/commands/UndoRedoStack.ts + Command.ts; keyboard bindings Cmd+Z / Cmd+Shift+Z.

STEP 5: Realtime pipeline
  - lib/realtime/signalRClient.ts (Task 03 contract, catch-up, LRU dedup);
    lib/streaming/eventBatcher.ts wired from the EntityChanged stream to cache patches.

STEP 6: Canvas DnD + palette + state boundaries
  - ZoomAwareDndContext.tsx; CommandPalette.tsx (cmdk); stateBoundaries.ts documenting §2.1.

STEP 7: Verification suite
  - Vitest unit tests (§5.1) + Playwright keyboard/optimistic-flow specs.
```

---

## 5. Test Plan, Verification Matrix & Acceptance Commands

All unit tests live under `src/Frontend/tests/` (the test root for this task and Task 06). Each test gets a fresh `QueryClient` and a mocked network — no shared state.

### 5.1 Unit & Integration Test Specifications

#### 1. Optimistic Mutation & 409 Flow (`tests/mutations/entityMutations.test.ts`)

```typescript
import { describe, it, expect, vi } from 'vitest';
import { QueryClient } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { useUpdateDelivery } from '../../src/lib/mutations/entityMutations';
import { createQueryWrapper } from '../helpers/createQueryWrapper';

describe('useUpdateDelivery optimistic flow', () => {
  it('applies the optimistic update immediately, then reconciles on success', async () => {
    // fetch mock resolves 200; assert cache shows the edit before resolution,
    // and invalidateQueries runs on settle.
  });

  it('rolls back the cache and surfaces the conflict dialog on HTTP 409', async () => {
    const queryClient = new QueryClient();
    queryClient.setQueryData(['deliveries'], [{ id: 'del-1', priceEurMwh: 34.5, version: 7 }]);

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ title: 'Conflict' }), { status: 409 })));

    const onConflict = vi.fn();
    const { result } = renderHook(() => useUpdateDelivery(onConflict), {
      wrapper: createQueryWrapper(queryClient)
    });

    result.current.mutate({ id: 'del-1', version: 7, changes: { priceEurMwh: 40.0 } });

    await waitFor(() => expect(onConflict).toHaveBeenCalledWith('del-1'));
    // Rolled back — the attempted edit must NOT survive (no silent client-wins).
    const rows = queryClient.getQueryData<any[]>(['deliveries'])!;
    expect(rows[0].priceEurMwh).toBe(34.5);
  });

  it('retries network errors up to 3 times, then rolls back and toasts', async () => {
    // fetch mock rejects (TypeError); assert 4 total calls, final cache === snapshot.
  });
});
```

#### 2. UndoRedoStack (`tests/commands/UndoRedoStack.test.ts`)
- `pushAndExecute` runs `execute()` and clears the redo branch.
- `undo()` calls the command's `undo()` (inverse mutation) and moves it to the redo stack.
- A rejecting `undo()` (simulated 409) discards the entry and returns `false`.
- Stack is bounded by `maxSize`.

#### 3. RxJS Event Coalescer (`tests/streaming/eventBatcher.test.ts`)

```typescript
import { describe, it, expect } from 'vitest';
import { EntityEventBatcher } from '../../src/lib/streaming/eventBatcher';

describe('EntityEventBatcher', () => {
  it('keeps only the highest-sequenceId event per aggregate within a window', () => {
    const batcher = new EntityEventBatcher(50);
    const events: any[] = [
      { aggregateType: 'PhysicalDelivery', aggregateId: '1', sequenceId: 1,
        payloadJson: '{"status":"Pending - No Invoice"}' },
      { aggregateType: 'PhysicalDelivery', aggregateId: '1', sequenceId: 2,
        payloadJson: '{"status":"Invoice Received"}' }
    ];
    const coalesced = batcher.coalesceEventBatch(events);
    expect(coalesced).toHaveLength(1);
    expect(coalesced[0].sequenceId).toBe(2);
  });

  it('emits one coalesced batch per bufferTime window (fake timers)', () => {
    // vi.useFakeTimers(); push a burst; advance 50ms; assert single callback
    // whose batch is coalesced. Asserts coalescing behavior, not throughput (D10).
  });
});
```

#### 4. SignalR Catch-Up & Dedup (`tests/realtime/signalRCatchUp.test.ts`)
- Catch-up pages `GET /api/v1/events?afterSequence={n}&limit=500` until a short page (mock two full pages + one short page → three requests).
- Duplicate `eventId`s (live + catch-up overlap) are delivered to `onEvent` exactly once (LRU dedup).
- `lastSequenceId` advances to the highest seen value and is used for the next catch-up.

### 5.2 Terminal Verification Commands

```bash
# 1. Install & build
cd src/Frontend
npm install
npm run build

# 2. Vitest unit suite (test root: src/Frontend/tests/)
npm run test -- --run

# 3. Lint
npm run lint

# 4. Playwright optimistic-flow & keyboard specs
npx playwright test tests/e2e/snappy-ui.spec.ts
```

---

## 6. Anti-Cheating & Integrity Guardrails

1. **NO silent client-wins**: on HTTP 409 the optimistic edit MUST be rolled back and the conflict dialog shown. Any auto-merge of conflicting server/client state is a violation (D5).
2. **NO version stripping**: every mutating request carries the entity's `version`. Omitting it (or hardcoding it) defeats OCC and fails audit.
3. **NO synthetic timer bypasses**: the `bufferTime(50)` coalescer must run genuine RxJS pipe operations; tests may use fake timers, but invoking callbacks directly without the operator chain is prohibited.
4. **NO fake catch-up/dedup**: the reconnect path must genuinely page `/api/v1/events` and dedup by `eventId`; stubs that unconditionally report "caught up" fail verification.
5. **NO virtual canvas hacks**: `ZoomAwareDndContext` must compute `transform.x / zoom` and `transform.y / zoom` from the live `useViewport()` value; hardcoded `1.0` scale factors are prohibited.
6. **Hermetic tests**: fresh `QueryClient` and mocked network per test; no cross-test state.

---
*Task 05 Detailed Implementation Specification File Complete.*
