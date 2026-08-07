import type { DragEndEvent } from '@dnd-kit/core';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import type { Dispatch, ReactNode, SetStateAction } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

interface CapturedFlowProps {
  nodes: Array<{ id: string; position: { x: number; y: number } }>;
  nodesDraggable: boolean;
  nodeTypes: Record<string, unknown>;
}

const captures = vi.hoisted(() => ({
  flowProps: undefined as CapturedFlowProps | undefined,
  onDragEnd: undefined as ((event: DragEndEvent) => void) | undefined
}));

vi.mock('@xyflow/react', async () => {
  const React = await import('react');
  return {
    addEdge: (connection: object, edges: object[]) => [...edges, { ...connection, id: `edge-${edges.length + 1}` }],
    Background: () => null,
    Controls: () => null,
    Handle: () => null,
    Position: { Left: 'left', Right: 'right' },
    ReactFlow: (props: CapturedFlowProps & { children?: ReactNode }) => {
      captures.flowProps = props;
      return React.createElement('div', { 'data-testid': 'react-flow' });
    },
    ReactFlowProvider: ({ children }: { children: ReactNode }) => React.createElement('div', { 'data-testid': 'react-flow-provider' }, children),
    useEdgesState: <T,>(initial: T[]) => {
      const [edges, setEdges] = React.useState(initial);
      return [edges, setEdges, () => undefined] as [T[], Dispatch<SetStateAction<T[]>>, () => void];
    },
    useNodesState: <T,>(initial: T[]) => {
      const [nodes, setNodes] = React.useState(initial);
      return [nodes, setNodes, () => undefined] as [T[], Dispatch<SetStateAction<T[]>>, () => void];
    }
  };
});

vi.mock('../../src/components/canvas/ZoomAwareDndContext', async () => {
  const React = await import('react');
  return {
    ZoomAwareDndContext: ({ children, onDragEnd }: { children: ReactNode; onDragEnd: (event: DragEndEvent) => void }) => {
      captures.onDragEnd = onDragEnd;
      return React.createElement('div', { 'data-testid': 'zoom-aware-dnd' }, children);
    }
  };
});

import { applyWorkflowNodeDrag, WorkflowCanvas, type WorkflowNode } from '../../src/components/canvas/WorkflowCanvas';

function dragEnd(id: string, x: number, y: number, kind = 'workflow-node'): DragEndEvent {
  return {
    active: { id, data: { current: { kind } } },
    delta: { x, y }
  } as unknown as DragEndEvent;
}

afterEach(() => {
  cleanup();
  captures.flowProps = undefined;
  captures.onDragEnd = undefined;
});

describe('WorkflowCanvas', () => {
  it('applies the zoom-adjusted drag delta only to workflow nodes', () => {
    const nodes: WorkflowNode[] = [
      { id: 'step-1', type: 'workflowStep', position: { x: 10, y: 20 }, data: { label: 'One' } },
      { id: 'step-2', type: 'workflowStep', position: { x: 50, y: 60 }, data: { label: 'Two' } }
    ];

    const moved = applyWorkflowNodeDrag(nodes, dragEnd('step-1', 15, -5));
    expect(moved[0].position).toEqual({ x: 25, y: 15 });
    expect(moved[1]).toBe(nodes[1]);
    expect(applyWorkflowNodeDrag(nodes, dragEnd('step-1', 99, 99, 'other'))).toBe(nodes);
  });

  it('provides React Flow with a typed zoom-aware drag caller and usable controls', () => {
    render(<WorkflowCanvas />);

    expect(screen.getByTestId('react-flow-provider')).toBeTruthy();
    expect(screen.getByTestId('zoom-aware-dnd')).toBeTruthy();
    expect(screen.getByTestId('react-flow')).toBeTruthy();
    expect(captures.flowProps?.nodesDraggable).toBe(false);
    expect(captures.flowProps?.nodeTypes).toHaveProperty('workflowStep');
    expect(captures.flowProps?.nodes).toHaveLength(2);

    act(() => captures.onDragEnd?.(dragEnd('step-1', 30, 12)));
    expect(captures.flowProps?.nodes[0].position).toEqual({ x: 70, y: 112 });

    fireEvent.click(screen.getByRole('button', { name: 'Add step' }));
    expect(captures.flowProps?.nodes).toHaveLength(3);
  });
});
