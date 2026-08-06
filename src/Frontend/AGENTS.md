# Frontend Guide

- Use React 19 and TypeScript.
- Import API contracts only from `src/api/generated`; regenerate them from C# DTOs rather than editing output.
- Use MSW 2 handlers from `src/mocks` for frontend tests.
- Keep feature imports within the ESLint boundaries configuration.
