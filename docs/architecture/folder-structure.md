# Recommended Folder & Directory Structure

> **LEGACY — DO NOT IMPLEMENT (marked 2026-08-06).** This layout targets the abandoned Iteration 1 SurrealDB stack. The authoritative layout is the monorepo structure in [`decision-log.md`](decision-log.md) D12 and the task specs (`src/Backend`, `src/Frontend`, `src/Database`, `infra/`, `tests/`).

*Part of the [Tradebook architecture plan](../README.md).*

## 3. Recommended Folder & Directory Structure

### Frontend Structure (`/src`)
```text
src/
├── app/                  # TanStack Router route tree & app setup
│   ├── routes/           # File-based routes (__root.tsx, index.tsx, dashboard.tsx)
│   └── provider.tsx      # Global providers (Auth, Theme, Query)
├── components/
│   ├── ui/               # ShadCN UI components (Button, Dialog, Input...)
│   ├── motion/           # Aceternity UI / Animate UI effect components
│   ├── canvas/           # React Flow custom nodes, controls, and edges
│   └── virtual/          # TanStack Virtual table & list wrappers
├── features/             # Domain-specific feature modules
│   ├── workflow/         # XState machines & workflow canvas components
│   ├── kanban/           # dnd-kit boards & card components
│   └── analytics/        # Real-time dashboard widgets
├── hooks/                # Global custom hooks (useSurreal, useLiveQuery)
├── lib/                  # Utility functions (cn, surreal-client, theme)
└── store/                # Zustand global stores (useAuthStore, useUIStore)
```

### Backend Structure (`/backend/src`)
```text
src/
├── Core/                 # Cross-cutting abstractions & SurrealDB extensions
│   ├── Auth/             # JWT generation & claim handlers
│   └── Database/         # SurrealDB connection factory & setup
└── Features/             # Vertical Slices (REPR Pattern)
    ├── Auth/
    │   ├── LoginEndpoint.cs
    │   ├── LoginRequest.cs
    │   ├── LoginResponse.cs
    │   └── LoginValidator.cs
    ├── Projects/
    │   ├── CreateProjectEndpoint.cs
    │   └── ProcessProjectBackgroundJob.cs
    └── Workflows/
        └── ExecuteWorkflowEndpoint.cs
```
