# Recommended Folder & Directory Structure — ⚠️ LEGACY (Iteration 1, SurrealDB-era; current stack is .NET 9 + PostgreSQL + React 19 per `master-architecture-blueprint.md`)

Part of Tradebook architecture plan.

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
