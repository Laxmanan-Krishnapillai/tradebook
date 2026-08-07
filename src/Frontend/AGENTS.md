# Frontend Guide

- Use React 19 and TypeScript.
- Import API contracts only from `src/api/generated`; regenerate them from C# DTOs rather than editing output.
- Use MSW 2 handlers from `src/mocks` for frontend tests.
- Keep feature imports within `.eslintrc.cjs`: feature components may consume shared UI,
  hooks, libraries, types, and generated contracts, but may not import sibling features.
- Code under `src/lib` may depend only on other library modules, types, generated
  contracts, and workers. Framework-independent chart mapping belongs in
  `src/lib/charts/visualEncodingMapper.ts`, never under a component folder.
- Define application URLs as typed TanStack file routes under `src/app/routes`; never
  add a parallel History API path switch. Regenerate `src/app/routeTree.gen.ts` through Vite.
- Keep server state in TanStack Query with factories from `src/lib/query/queryKeys.ts`.
  The session controller owns cache cancellation/clearing at login, logout, token, and actor boundaries.
- Keep Zustand limited to ephemeral shared UI state. The global modal is owned by
  `src/lib/state/useUiStore.ts`; do not bridge component state with DOM custom events.
