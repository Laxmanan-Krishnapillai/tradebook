# Task 05: React 19 Local-First Optimistic Snappy CRUD UI & TanStack DB Integration

- **Phase**: Frontend UI & User Experience
- **Lead / Owner**: Frontend Lead Architect
- **Complexity**: Very High
- **Prerequisites**: Task 02 (.NET 9 Modular Monolith Backend Core), Task 03 (SignalR Core Real-Time Engine), Task 08 (Agent-Readiness & TypeGen Contracts)
- **Target Files**:
  - `src/Frontend/package.json`
  - `src/Frontend/vite.config.ts`
  - `src/Frontend/src/lib/sync/db.ts`
  - `src/Frontend/src/lib/sync/mutationQueue.ts`
  - `src/Frontend/src/lib/commands/UndoRedoStack.ts`
  - `src/Frontend/src/lib/streaming/eventBatcher.ts`
  - `src/Frontend/src/components/canvas/ZoomAwareDndContext.tsx`
  - `src/Frontend/src/components/ui/CommandPalette.tsx`
  - `src/Frontend/src/lib/state/stateBoundaries.ts`
  - `src/Frontend/src/components/grid/VirtualizedDataTable.tsx`
  - `src/Frontend/tests/sync/mutationQueue.test.ts`
  - `src/Frontend/tests/commands/UndoRedoStack.test.ts`
  - `src/Frontend/tests/streaming/eventBatcher.test.ts`
  - `src/Frontend/tests/canvas/ZoomAwareDndContext.test.tsx`

---

## Executive Overview

Task 05 defines the full architecture and implementation blueprint for Tradebook's **React 19 Local-First Optimistic Snappy CRUD UI & TanStack DB Integration** — a snappy, sub-16ms perceived latency, keyboard-first web interface comparable to Linear, Twenty CRM, and Figma.

It integrates an offline-capable Dexie.js IndexedDB mutation queue with offline compaction and batch REST sync (`POST /api/v1/mutations/batch`), a command-pattern `UndoRedoStack` class supporting structural 3-way entity merges, an RxJS `bufferTime(50)` sliding-window event batcher for throttling high-frequency WebSocket streams, scale-synchronized drag-and-drop canvas editing via a custom `ZoomAwareDndContext`, and a Kbar global command palette.

```
+---------------------------------------------------------------------------------------------------+
|                                 REACT 19 LOCAL-FIRST UI ARCHITECTURE                              |
+---------------------------------------------------------------------------------------------------+
|                                                                                                   |
|   +-------------------------------------------------------------------------------------------+   |
|   |                            React 19 Single Page Application (SPA)                         |   |
|   |  - React 19 Actions (`useActionState`, `useOptimistic`, `useTransition`)                  |   |
|   |  - State Partitioning: Zustand (UI) | XState (FSMs) | TanStack Query/DB (Server Cache)   |   |
|   +-------------------------------------------------------------------------------------------+   |
|            │                                            │                                 ▲       |
|            │ (0ms Local Optimistic Mutation)            │ (Background Batch REST Sync)    │       |
|            ▼                                            ▼                                 │       |
|   +--------------------------+         +----------------------------------+               │       |
|   | TanStack Query / DB      |         | Dexie.js IndexedDB Queue         |               │       |
|   | Local In-Memory Cache    |         | - Status: PENDING / SYNCING      |               │       |
|   | (Renders instantly <16ms)|         | - Compaction: Coalesces Edits    |               │       |
|   +--------------------------+         +----------------------------------+               │       |
|                                                         │                                 │       |
|                                                         │ POST /api/v1/mutations/batch    │       |
|                                                         v                                 │       |
|                                        +----------------------------------+               │       |
|                                        | .NET 9 Backend API & Postgres    |               │       |
|                                        +----------------------------------+               │       |
|                                                         │                                 │       |
|                                                         │ SignalR MessagePack Push        │       |
|                                                         v                                 │       |
|                                        +----------------------------------+               │       |
|                                        | RxJS bufferTime(50) Batcher      |────────────────┘       |
|                                        | (Coalesces 5,000 msgs/sec -> 20) |                       |
|                                        +----------------------------------+                       |
|                                                                                                   |
+---------------------------------------------------------------------------------------------------+
```

---

## 1. Objectives, Scope, Dependencies & Prerequisites

### 1.1 Core Objectives
1. **0ms Perceived Latency Mutations**: Immediate visual feedback for all user mutations (grid cell edit, kanban drag, workflow node modification) via optimistic writes to TanStack Query / TanStack DB cache before network round-trips.
2. **Durable Offline Mutation Queue**: Persist pending user actions to IndexedDB via Dexie.js. Mutations survive browser refreshes or extended offline states and auto-flush via `POST /api/v1/mutations/batch` on network restoration.
3. **Offline Mutation Compaction**: Coalesce sequential mutations for identical entities before server transmission to minimize network payloads and prevent server-side race conditions.
4. **Command Pattern & Revertability**: A robust `UndoRedoStack` supports `Cmd+Z` / `Cmd+Shift+Z` with a deterministic 3-way merge algorithm to resolve background server reconciliation conflicts cleanly.
5. **High-Throughput WebSocket Event Batching**: Incoming SignalR binary MessagePack streams buffer via an RxJS `bufferTime(50)` sliding window, preventing main-thread stuttering during market bursts of up to 5,000 updates/sec.
6. **Scale-Synchronized Canvas Drag-and-Drop**: Resolves the React Flow canvas zoom scale desynchronization bug via a custom `ZoomAwareDndContext` and translation modifier.
7. **Keyboard-First Ergonomics**: A global Kbar command palette supports modal actions, navigation shortcuts (`g p`, `g o`), and fuzzy search.

### 1.2 Prerequisites & Dependencies
- **Task 02 (.NET Backend Core)**: API endpoints for `/api/v1/mutations/batch` and entity CRUD.
- **Task 03 (SignalR Real-Time Engine)**: Binary MessagePack WebSocket hubs streaming entity change notifications.
- **Task 08 (TypeGen Contracts)**: Automated TypeScript DTOs in `src/Frontend/src/types/generated/`.
- **Frontend Core Packages**:
  - `react@^19.0.0`, `react-dom@^19.0.0`
  - `@tanstack/react-query@^5.0.0`, `@tanstack/react-table@^8.0.0`
  - `dexie@^4.0.0`, `dexie-react-hooks@^1.1.0`
  - `rxjs@^7.8.0`
  - `@xyflow/react@^12.0.0` (React Flow)
  - `@dnd-kit/core@^6.1.0`, `@dnd-kit/modifiers@^7.0.0`
  - `kbar@^0.1.0-beta.45`
  - `zustand@^4.5.0`, `xstate@^5.10.0`

---

## 2. Architecture & State Boundaries Matrix

### 2.1 State Boundary Allocation

To eliminate state duplication and prevent re-render cascading, Tradebook enforces a strict **State Boundary Allocation Matrix**:

| State Category | Responsible Library | Storage Location | Lifetime & Scope | Example Usage |
| :--- | :--- | :--- | :--- | :--- |
| **Server Entity Cache** | TanStack Query v5 / TanStack DB | In-Memory QueryCache | Session / Server-Synced | Physical deliveries, contracts, capacity bookings, market price indexes |
| **Durable Offline Log** | Dexie.js | IndexedDB (`tradebook_db`) | Persistent across sessions | Pending mutations (`status: 'PENDING'`), offline drafts |
| **Global Ephemeral UI** | Zustand | Memory (`useUIStore`) | Session / Memory-only | Sidebar collapse, active modal ID, focused table cell, dark mode |
| **Workflow State Machine** | XState v5 | Memory (`useActor`) | Canvas Component Lifecycle | Multi-step canvas node creation, connector linking FSM |
| **Command History** | `UndoRedoStack` | Memory (`useCommandStore`) | Workspace Session | Undo/Redo command buffer (`Cmd+Z` history) |

### 2.2 React 19 CSR SPA Component Hierarchy

```
src/Frontend/src/
├── assets/
├── components/
│   ├── canvas/
│   │   ├── WorkflowCanvas.tsx
│   │   ├── ZoomAwareDndContext.tsx
│   │   └── ZoomAwareDragOverlay.tsx
│   ├── grid/
│   │   ├── VirtualizedDataTable.tsx
│   │   ├── GridCellEditors.tsx
│   │   └── GridToolbar.tsx
│   ├── layout/
│   │   ├── AppHeader.tsx
│   │   ├── AppLayout.tsx
│   │   └── Sidebar.tsx
│   └── ui/
│       ├── CommandPalette.tsx
│       └── StatusBadge.tsx
├── hooks/
│   ├── useCommandStack.ts
│   ├── useMutationQueue.ts
│   ├── useSignalRStream.ts
│   └── useVirtualizedTable.ts
├── lib/
│   ├── commands/
│   │   ├── Command.ts
│   │   └── UndoRedoStack.ts
│   ├── state/
│   │   ├── stateBoundaries.ts
│   │   ├── useUIStore.ts
│   │   └── workflowFSM.ts
│   ├── streaming/
│   │   └── eventBatcher.ts
│   └── sync/
│       ├── db.ts
│       └── mutationQueue.ts
├── types/
│   └── generated/             <-- Auto-generated by TypeGen (Task 08)
├── App.tsx
├── main.tsx
└── vite.config.ts
```

---

## 3. Implementation Code Contracts & Blueprints

### 3.1 Dexie.js IndexedDB Schema & Database Initialization (`src/Frontend/src/lib/sync/db.ts`)

```typescript
import Dexie, { Table } from 'dexie';

export interface LocalMutationRecord {
  id?: number;
  mutationId: string; // Unique ULID/UUID for tracing
  entityType: 'PHYSICAL_DELIVERY' | 'CAPACITY_BOOKING' | 'TRANSFER' | 'BIOTICKET_DELIVERY' | 'GOO_CERTIFICATE_TRANSACTION' | 'CONTRACT' | 'CANVAS_NODE';
  entityId: string;
  operation: 'INSERT' | 'UPDATE' | 'DELETE';
  payload: Record<string, any>;
  baseVersion: number; // Optimistic concurrency control version
  status: 'PENDING' | 'SYNCING' | 'FAILED';
  retryCount: number;
  errorMessage?: string;
  createdAt: number;
  updatedAt: number;
}

export class TradebookLocalDB extends Dexie {
  mutations!: Table<LocalMutationRecord, number>;

  constructor() {
    super('TradebookLocalDB');
    this.version(1).stores({
      mutations: '++id, mutationId, [entityType+entityId], status, createdAt'
    });
  }
}

export const localDB = new TradebookLocalDB();
```

### 3.2 Dexie.js Offline Mutation Queue & Batch Compactor (`src/Frontend/src/lib/sync/mutationQueue.ts`)

```typescript
import { localDB, LocalMutationRecord } from './db';
import { QueryClient } from '@tanstack/react-query';

export interface BatchMutationRequestDto {
  mutations: Array<{
    mutationId: string;
    entityType: string;
    entityId: string;
    operation: 'INSERT' | 'UPDATE' | 'DELETE';
    payload: Record<string, any>;
    baseVersion: number;
  }>;
}

export class OfflineMutationQueueManager {
  private queryClient: QueryClient;
  private isSyncing = false;
  private syncIntervalId: number | null = null;

  constructor(queryClient: QueryClient) {
    this.queryClient = queryClient;
  }

  public async enqueue(
    entityType: LocalMutationRecord['entityType'],
    entityId: string,
    operation: LocalMutationRecord['operation'],
    payload: Record<string, any>,
    baseVersion: number
  ): Promise<string> {
    const mutationId = crypto.randomUUID();
    const now = Date.now();

    await localDB.transaction('rw', localDB.mutations, async () => {
      await localDB.mutations.add({
        mutationId,
        entityType,
        entityId,
        operation,
        payload,
        baseVersion,
        status: 'PENDING',
        retryCount: 0,
        createdAt: now,
        updatedAt: now
      });
    });

    // Optimistically update TanStack Query cache immediately
    this.applyOptimisticUpdate(entityType, entityId, operation, payload);

    // Trigger background flush if online
    if (navigator.onLine) {
      this.flushQueue();
    }

    return mutationId;
  }

  /**
   * Compacts pending mutations for identical (entityType, entityId) pairs.
   * Compaction rules:
   * 1. INSERT followed by UPDATE -> Single INSERT with merged payload.
   * 2. INSERT followed by DELETE -> Remove both from queue (no-op).
   * 3. UPDATE followed by UPDATE -> Single UPDATE with shallow-merged payload.
   * 4. UPDATE followed by DELETE -> Single DELETE mutation.
   */
  public compactMutations(mutations: LocalMutationRecord[]): LocalMutationRecord[] {
    const map = new Map<string, LocalMutationRecord>();

    for (const item of mutations) {
      const key = `${item.entityType}:${item.entityId}`;
      const existing = map.get(key);

      if (!existing) {
        map.set(key, { ...item });
        continue;
      }

      if (existing.operation === 'INSERT' && item.operation === 'UPDATE') {
        existing.payload = { ...existing.payload, ...item.payload };
        existing.updatedAt = item.createdAt;
      } else if (existing.operation === 'INSERT' && item.operation === 'DELETE') {
        map.delete(key);
      } else if (existing.operation === 'UPDATE' && item.operation === 'UPDATE') {
        existing.payload = { ...existing.payload, ...item.payload };
        existing.updatedAt = item.createdAt;
      } else if (existing.operation === 'UPDATE' && item.operation === 'DELETE') {
        existing.operation = 'DELETE';
        existing.payload = item.payload;
        existing.updatedAt = item.createdAt;
      } else {
        map.set(key, { ...item });
      }
    }

    return Array.from(map.values());
  }

  public async flushQueue(): Promise<void> {
    if (this.isSyncing || !navigator.onLine) return;
    this.isSyncing = true;

    try {
      const pending = await localDB.mutations
        .where('status')
        .equals('PENDING')
        .sortBy('createdAt');

      if (pending.length === 0) {
        this.isSyncing = false;
        return;
      }

      const compacted = this.compactMutations(pending);

      // Mark status as SYNCING in IndexedDB
      const idsToUpdate = pending.map((m) => m.id!).filter(Boolean);
      await localDB.mutations
        .where('id')
        .anyOf(idsToUpdate)
        .modify({ status: 'SYNCING' });

      const dto: BatchMutationRequestDto = {
        mutations: compacted.map((m) => ({
          mutationId: m.mutationId,
          entityType: m.entityType,
          entityId: m.entityId,
          operation: m.operation,
          payload: m.payload,
          baseVersion: m.baseVersion
        }))
      };

      const response = await fetch('/api/v1/mutations/batch', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(dto)
      });

      if (!response.ok) {
        throw new Error(`Batch sync HTTP ${response.status}`);
      }

      // Sync successful: remove synced items from Dexie queue
      await localDB.mutations.where('id').anyOf(idsToUpdate).delete();

      // Invalidate relevant server queries to reconcile final state
      this.queryClient.invalidateQueries({ queryKey: ['entities'] });
    } catch (err: any) {
      // Revert SYNCING items back to PENDING and increment retry count
      await localDB.mutations
        .where('status')
        .equals('SYNCING')
        .modify((m) => {
          m.status = 'PENDING';
          m.retryCount += 1;
          m.errorMessage = err?.message || 'Sync failed';
        });
    } finally {
      this.isSyncing = false;
    }
  }

  private applyOptimisticUpdate(
    entityType: string,
    entityId: string,
    operation: string,
    payload: Record<string, any>
  ): void {
    const queryKey = ['entities', entityType];
    this.queryClient.setQueryData<any[]>(queryKey, (oldData = []) => {
      if (operation === 'INSERT') {
        return [...oldData, { id: entityId, ...payload }];
      }
      if (operation === 'UPDATE') {
        return oldData.map((item) =>
          item.id === entityId ? { ...item, ...payload } : item
        );
      }
      if (operation === 'DELETE') {
        return oldData.filter((item) => item.id !== entityId);
      }
      return oldData;
    });
  }
}
```

### 3.3 Command Pattern `UndoRedoStack` & 3-Way Structural Merge (`src/Frontend/src/lib/commands/UndoRedoStack.ts`)

```typescript
export interface Command<T = any> {
  id: string;
  description: string;
  timestamp: number;
  execute(): Promise<void>;
  undo(): Promise<void>;
  redo(): Promise<void>;
}

export interface MergeResult<T> {
  mergedState: T;
  hasConflict: boolean;
  conflictingFields: string[];
}

export class UndoRedoStack {
  private undoStack: Command[] = [];
  private redoStack: Command[] = [];
  private maxSize: number;

  constructor(maxSize = 100) {
    this.maxSize = maxSize;
  }

  public async pushAndExecute(command: Command): Promise<void> {
    await command.execute();
    this.undoStack.push(command);
    if (this.undoStack.length > this.maxSize) {
      this.undoStack.shift();
    }
    this.redoStack = []; // Clear redo stack on new command execution
  }

  public async undo(): Promise<boolean> {
    const cmd = this.undoStack.pop();
    if (!cmd) return false;
    await cmd.undo();
    this.redoStack.push(cmd);
    return true;
  }

  public async redo(): Promise<boolean> {
    const cmd = this.redoStack.pop();
    if (!cmd) return false;
    await cmd.redo();
    this.undoStack.push(cmd);
    return true;
  }

  public canUndo(): boolean {
    return this.undoStack.length > 0;
  }

  public canRedo(): boolean {
    return this.redoStack.length > 0;
  }

  /**
   * Structural 3-Way Entity Merge Algorithm
   * Compares Base Ancestor State (B), Local Optimistic State (L), and Remote Server State (R).
   * Rules per field:
   * 1. L === B and R !== B => Apply Remote (R)
   * 2. R === B and L !== B => Retain Local (L)
   * 3. L === R => Retain value
   * 4. L !== R and L !== B and R !== B => Flag field conflict; retain Local with conflict audit.
   */
  public perform3WayMerge<T extends Record<string, any>>(
    baseState: T,
    localState: T,
    remoteState: T
  ): MergeResult<T> {
    const mergedState: Record<string, any> = { ...baseState };
    const conflictingFields: string[] = [];

    const allKeys = Array.from(
      new Set([
        ...Object.keys(baseState),
        ...Object.keys(localState),
        ...Object.keys(remoteState)
      ])
    );

    for (const key of allKeys) {
      const baseVal = baseState[key];
      const localVal = localState[key];
      const remoteVal = remoteState[key];

      const localChanged = JSON.stringify(localVal) !== JSON.stringify(baseVal);
      const remoteChanged = JSON.stringify(remoteVal) !== JSON.stringify(baseVal);

      if (!localChanged && remoteChanged) {
        mergedState[key] = remoteVal;
      } else if (localChanged && !remoteChanged) {
        mergedState[key] = localVal;
      } else if (JSON.stringify(localVal) === JSON.stringify(remoteVal)) {
        mergedState[key] = localVal;
      } else {
        // Conflict detected
        mergedState[key] = localVal; // Keep local value optimistically
        conflictingFields.push(key);
      }
    }

    return {
      mergedState: mergedState as T,
      hasConflict: conflictingFields.length > 0,
      conflictingFields
    };
  }
}
```

### 3.4 RxJS `bufferTime(50)` Sliding-Window WebSocket Event Batcher (`src/Frontend/src/lib/streaming/eventBatcher.ts`)

```typescript
import { Subject, Subscription } from 'rxjs';
import { bufferTime, filter, map } from 'rxjs/operators';

export interface EntityStreamEvent {
  entityType: string;
  entityId: string;
  operation: 'INSERT' | 'UPDATE' | 'DELETE';
  payload: Record<string, any>;
  sequenceNumber: number;
  timestamp: number;
}

export class WebSocketEventBatcher {
  private inputStream$ = new Subject<EntityStreamEvent>();
  private subscription: Subscription | null = null;
  private windowTimeMs: number;

  constructor(windowTimeMs = 50) {
    this.windowTimeMs = windowTimeMs;
  }

  public start(onBatchReady: (compactedBatch: EntityStreamEvent[]) => void): void {
    this.subscription = this.inputStream$
      .pipe(
        bufferTime(this.windowTimeMs),
        filter((batch) => batch.length > 0),
        map((batch) => this.coalesceEventBatch(batch))
      )
      .subscribe((compactedBatch) => {
        onBatchReady(compactedBatch);
      });
  }

  public pushEvent(event: EntityStreamEvent): void {
    this.inputStream$.next(event);
  }

  /**
   * Coalesces high-frequency updates within a 50ms window.
   * If an entity receives 10 updates within 50ms, only the latest state is emitted.
   */
  public coalesceEventBatch(batch: EntityStreamEvent[]): EntityStreamEvent[] {
    const map = new Map<string, EntityStreamEvent>();

    for (const event of batch) {
      const key = `${event.entityType}:${event.entityId}`;
      const existing = map.get(key);

      if (!existing || event.sequenceNumber > existing.sequenceNumber) {
        map.set(key, event);
      }
    }

    return Array.from(map.values());
  }

  public stop(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
      this.subscription = null;
    }
  }
}
```

### 3.5 React Flow + dnd-kit `ZoomAwareDndContext` (`src/Frontend/src/components/canvas/ZoomAwareDndContext.tsx`)

```tsx
import React, { ReactNode } from 'react';
import { DndContext, Modifier, DragOverlay, useDndContext } from '@dnd-kit/core';
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

### 3.6 Kbar Command Palette Component (`src/Frontend/src/components/ui/CommandPalette.tsx`)

```tsx
import React from 'react';
import {
  KBarProvider,
  KBarPortal,
  KBarPositioner,
  KBarAnimator,
  KBarSearch,
  useMatches,
  Action
} from 'kbar';
import { useNavigate } from 'react-router-dom';

const searchStyle: React.CSSProperties = {
  padding: '12px 16px',
  fontSize: '16px',
  width: '100%',
  boxSizing: 'border-box',
  outline: 'none',
  border: 'none',
  background: 'var(--bg-secondary)',
  color: 'var(--text-primary)'
};

const animatorStyle: React.CSSProperties = {
  maxWidth: '600px',
  width: '100%',
  background: '#1e1e2e',
  color: '#cdd6f4',
  borderRadius: '8px',
  overflow: 'hidden',
  boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.5)'
};

export const CommandPaletteProvider: React.FC<{ children: React.ReactNode }> = ({
  children
}) => {
  const navigate = useNavigate();

  const actions: Action[] = [
    {
      id: 'go-deliveries',
      name: 'Go to Deliveries',
      shortcut: ['g', 'd'],
      keywords: 'physical deliveries book records',
      perform: () => navigate('/deliveries')
    },
    {
      id: 'go-contracts',
      name: 'Go to Contracts',
      shortcut: ['g', 'c'],
      keywords: 'contracts instances master data',
      perform: () => navigate('/contracts')
    },
    {
      id: 'go-market-prices',
      name: 'Go to Market Prices',
      shortcut: ['g', 'm'],
      keywords: 'ttf index fx rates market prices',
      perform: () => navigate('/market-prices')
    },
    {
      id: 'create-delivery',
      name: 'Create New Physical Delivery',
      shortcut: ['c', 'd'],
      keywords: 'create delivery volume nomination',
      perform: () => window.dispatchEvent(new CustomEvent('open-create-delivery-modal'))
    }
  ];

  return (
    <KBarProvider actions={actions}>
      <KBarPortal>
        <KBarPositioner style={{ zIndex: 9999 }}>
          <KBarAnimator style={animatorStyle}>
            <KBarSearch style={searchStyle} placeholder="Type a command or search..." />
            <RenderResults />
          </KBarAnimator>
        </KBarPositioner>
      </KBarPortal>
      {children}
    </KBarProvider>
  );
};

function RenderResults() {
  const { results } = useMatches();

  return (
    <div style={{ paddingBottom: '8px', maxHeight: '400px', overflowY: 'auto' }}>
      {results.map((item, index) =>
        typeof item === 'string' ? (
          <div key={index} style={{ padding: '8px 16px', fontSize: '12px', opacity: 0.5 }}>
            {item}
          </div>
        ) : (
          <div
            key={item.id}
            onClick={item.perform as any}
            style={{
              padding: '10px 16px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              cursor: 'pointer'
            }}
          >
            <span>{item.name}</span>
            {item.shortcut?.length ? (
              <span style={{ display: 'flex', gap: '4px' }}>
                {item.shortcut.map((sc) => (
                  <kbd
                    key={sc}
                    style={{
                      background: '#313244',
                      padding: '2px 6px',
                      borderRadius: '4px',
                      fontSize: '11px'
                    }}
                  >
                    {sc}
                  </kbd>
                ))}
              </span>
            ) : null}
          </div>
        )
      )}
    </div>
  );
}
```

---

## 4. Subagent Implementation Step-by-Step Workflow

Subagent implementations must adhere strictly to the following 7-step sequence:

```
+---------------------------------------------------------------------------------------------------+
|                                 SUBAGENT STEP-BY-STEP WORKFLOW                                    |
+---------------------------------------------------------------------------------------------------+
|                                                                                                   |
|  STEP 1: Initialize Vite + React 19 Frontend Environment                                          |
|  - Validate `package.json` dependencies (`react@^19.0.0`, `@tanstack/react-query@^5.0.0`, etc.)  |
|  - Verify Vite TypeScript build config in `vite.config.ts`.                                       |
|                                                                                                   |
|  STEP 2: Construct Dexie.js Schema & Mutation Queue Manager                                       |
|  - Implement `src/Frontend/src/lib/sync/db.ts` with IndexedDB compound indexes.                   |
|  - Implement `src/Frontend/src/lib/sync/mutationQueue.ts` with `compactMutations` logic.          |
|                                                                                                   |
|  STEP 3: Author Command Pattern UndoRedoStack Class                                               |
|  - Implement `src/Frontend/src/lib/commands/UndoRedoStack.ts`.                                   |
|  - Integrate `perform3WayMerge` algorithm for conflict resolution.                                |
|                                                                                                   |
|  STEP 4: Implement RxJS WebSocket Event Batcher                                                   |
|  - Create `src/Frontend/src/lib/streaming/eventBatcher.ts` with `bufferTime(50)`.                 |
|  - Wire `coalesceEventBatch` to merge intra-window sequence numbers.                             |
|                                                                                                   |
|  STEP 5: Build ZoomAwareDndContext for React Flow                                                 |
|  - Implement `src/Frontend/src/components/canvas/ZoomAwareDndContext.tsx`.                         |
|  - Attach `createZoomModifier` inverse scale translator.                                          |
|                                                                                                   |
|  STEP 6: Assemble Kbar Command Palette & State Boundaries                                         |
|  - Create `src/Frontend/src/components/ui/CommandPalette.tsx`.                                   |
|  - Validate Zustand / XState / TanStack DB isolation in `stateBoundaries.ts`.                     |
|                                                                                                   |
|  STEP 7: Execute Vitest & Playwright Verification Suite                                           |
|  - Run unit test specifications for mutation queue, UndoRedoStack, and event batcher.             |
|  - Execute Playwright optimistic latency and keyboard command specs.                              |
|                                                                                                   |
+---------------------------------------------------------------------------------------------------+
```

---

## 5. Test Plan, Verification Matrix & Acceptance Commands

### 5.1 Unit & Integration Test Specifications

Subagents must author and execute the following unit test suites using Vitest and React Testing Library:

#### 1. Mutation Queue Compaction Test (`tests/sync/mutationQueue.test.ts`)
```typescript
import { describe, it, expect } from 'vitest';
import { OfflineMutationQueueManager } from '../../src/lib/sync/mutationQueue';

describe('OfflineMutationQueueManager Compaction', () => {
  it('should compact INSERT + UPDATE into a single merged INSERT', () => {
    const manager = new OfflineMutationQueueManager({} as any);
    const mockMutations: any[] = [
      { entityType: 'PHYSICAL_DELIVERY', entityId: 'del-1', operation: 'INSERT', payload: { priceEurMwh: 34.50 }, createdAt: 1 },
      { entityType: 'PHYSICAL_DELIVERY', entityId: 'del-1', operation: 'UPDATE', payload: { volumeRealisedMwh: 11840 }, createdAt: 2 }
    ];

    const compacted = manager.compactMutations(mockMutations);
    expect(compacted).toHaveLength(1);
    expect(compacted[0].operation).toBe('INSERT');
    expect(compacted[0].payload).toEqual({ priceEurMwh: 34.50, volumeRealisedMwh: 11840 });
  });

  it('should remove INSERT + DELETE for same entity (no-op)', () => {
    const manager = new OfflineMutationQueueManager({} as any);
    const mockMutations: any[] = [
      { entityType: 'PHYSICAL_DELIVERY', entityId: 'del-2', operation: 'INSERT', payload: { priceEurMwh: 40.00 }, createdAt: 1 },
      { entityType: 'PHYSICAL_DELIVERY', entityId: 'del-2', operation: 'DELETE', payload: {}, createdAt: 2 }
    ];

    const compacted = manager.compactMutations(mockMutations);
    expect(compacted).toHaveLength(0);
  });
});
```

#### 2. UndoRedoStack & 3-Way Merge Test (`tests/commands/UndoRedoStack.test.ts`)
```typescript
import { describe, it, expect } from 'vitest';
import { UndoRedoStack } from '../../src/lib/commands/UndoRedoStack';

describe('UndoRedoStack 3-Way Merge', () => {
  it('should retain local change when remote is unchanged', () => {
    const stack = new UndoRedoStack();
    const base = { id: '1', title: 'Original', status: 'PENDING' };
    const local = { id: '1', title: 'Updated Title', status: 'PENDING' };
    const remote = { id: '1', title: 'Original', status: 'PENDING' };

    const result = stack.perform3WayMerge(base, local, remote);
    expect(result.hasConflict).toBe(false);
    expect(result.mergedState.title).toBe('Updated Title');
  });

  it('should detect conflicting concurrent edits', () => {
    const stack = new UndoRedoStack();
    const base = { id: '1', price: 100 };
    const local = { id: '1', price: 150 };
    const remote = { id: '1', price: 200 };

    const result = stack.perform3WayMerge(base, local, remote);
    expect(result.hasConflict).toBe(true);
    expect(result.conflictingFields).toContain('price');
  });
});
```

#### 3. RxJS Event Batcher Test (`tests/streaming/eventBatcher.test.ts`)
```typescript
import { describe, it, expect, vi } from 'vitest';
import { WebSocketEventBatcher } from '../../src/lib/streaming/eventBatcher';

describe('WebSocketEventBatcher', () => {
  it('should coalesce high-frequency events within window', () => {
    const batcher = new WebSocketEventBatcher(50);
    const mockEvents: any[] = [
      { entityType: 'PHYSICAL_DELIVERY', entityId: '1', sequenceNumber: 1, payload: { status: 'Pending - No Invoice' } },
      { entityType: 'PHYSICAL_DELIVERY', entityId: '1', sequenceNumber: 2, payload: { status: 'Invoice Received' } }
    ];

    const coalesced = batcher.coalesceEventBatch(mockEvents);
    expect(coalesced).toHaveLength(1);
    expect(coalesced[0].sequenceNumber).toBe(2);
    expect(coalesced[0].payload.status).toBe('Invoice Received');
  });
});
```

### 5.2 Terminal Verification Commands

```bash
# 1. Install & Build Verification
cd src/Frontend
npm install
npm run build

# 2. Execute Vitest Unit Test Suite
npm run test -- --run

# 3. Execute Linting Rules
npm run lint

# 4. Playwright End-to-End Latency & Keyboard Command Tests
npx playwright test tests/e2e/snappy-ui.spec.ts
```

---

## 6. Anti-Cheating & Integrity Guardrails

To prevent facade implementations or artificial test passes, subagents and forensic auditors must enforce the following integrity rules:

1. **NO Hardcoded Mutation Responses**: All IndexedDB operations must write genuine records to Dexie.js (`TradebookLocalDB`). Mocking `Dexie` to return dummy static objects without reading/writing IndexedDB storage is strictly forbidden.
2. **NO Synthetic Timer Bypasses**: The RxJS `bufferTime(50)` sliding-window batcher must execute genuine RxJS pipe operations. Artificially invoking callbacks immediately without testing the temporal window function is prohibited.
3. **NO Ignored Conflict Branches**: The `perform3WayMerge` method must evaluate actual JSON-serialized object field comparisons. Fake return objects like `{ mergedState: localState, hasConflict: false }` without calculating field diffs will fail verification.
4. **NO Virtual Canvas Hacks**: The `ZoomAwareDndContext` must compute translation vectors dynamically using `transform.x / zoom` and `transform.y / zoom`. Hardcoding `1.0` scale factors or ignoring the `useViewport()` hook is prohibited.
5. **Hermetic Test Isolation**: All unit and integration tests must run in hermetic test environments with clean IndexedDB storage setups (using `fake-indexeddb` or Vitest browser mode).

---
*Task 05 Detailed Implementation Specification File Complete.*
