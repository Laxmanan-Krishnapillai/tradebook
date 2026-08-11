import { createContext } from 'react';
import { assign, setup, type ActorRefFrom } from 'xstate';

interface GridInteractionContext {
  activeCellId: string | undefined;
}

type GridInteractionEvent =
  | { type: 'cell.edit'; cellId: string }
  | { type: 'cell.close'; cellId: string }
  | { type: 'row.open'; row: object };

export const gridInteractionMachine = setup({
  types: {} as {
    context: GridInteractionContext;
    events: GridInteractionEvent;
  },
  actions: {
    activateCell: assign({
      activeCellId: ({ event }) => event.type === 'cell.edit' ? event.cellId : undefined,
    }),
    clearActiveCell: assign({ activeCellId: undefined }),
    openRow: () => undefined,
  },
  guards: {
    isActiveCell: ({ context, event }) => event.type === 'cell.close'
      && context.activeCellId === event.cellId,
  },
}).createMachine({
  id: 'gridInteraction',
  initial: 'idle',
  context: { activeCellId: undefined },
  states: {
    idle: {
      on: {
        'cell.edit': { target: 'editing', actions: 'activateCell' },
        'row.open': { target: 'opening' },
      },
    },
    editing: {
      on: {
        'cell.edit': { actions: 'activateCell' },
        'cell.close': {
          guard: 'isActiveCell',
          target: 'idle',
          actions: 'clearActiveCell',
        },
        'row.open': {
          target: 'idle',
          actions: 'clearActiveCell',
        },
      },
    },
    opening: {
      entry: 'openRow',
      always: 'idle',
    },
  },
});

export type GridInteractionActor = ActorRefFrom<typeof gridInteractionMachine>;

export const GridInteractionActorContext = createContext<GridInteractionActor | null>(null);
