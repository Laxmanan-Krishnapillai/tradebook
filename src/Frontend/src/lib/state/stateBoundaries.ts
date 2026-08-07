/** State boundaries: Query owns server data; Zustand owns ephemeral UI/auth; command and stream cursors stay in memory. */
export const stateBoundaries = {
  server: 'TanStack Query cache',
  ui: 'Zustand memory',
  auth: 'Zustand memory',
  commands: 'session-only memory',
  realtimeCursor: 'session-only memory'
} as const;
