import { useDraggable } from '@dnd-kit/core';
import type { DragEndEvent } from '@dnd-kit/core';
import {
  addEdge,
  Background,
  Controls,
  Handle,
  Position,
  ReactFlow,
  ReactFlowProvider,
  useEdgesState,
  useNodesState,
  type Connection,
  type Edge,
  type Node,
  type NodeProps,
  type NodeTypes
} from '@xyflow/react';
import { useCallback, useRef } from 'react';
import '@xyflow/react/dist/style.css';
import { ZoomAwareDndContext } from './ZoomAwareDndContext';
import './WorkflowCanvas.css';

const WORKFLOW_NODE_DRAG_KIND = 'workflow-node';

export type WorkflowNode = Node<{ label: string }, 'workflowStep'>;

const initialNodes: WorkflowNode[] = [
  { id: 'step-1', type: 'workflowStep', position: { x: 40, y: 100 }, data: { label: 'Nominate volume' } },
  { id: 'step-2', type: 'workflowStep', position: { x: 340, y: 100 }, data: { label: 'Approve delivery' } }
];

const initialEdges: Edge[] = [
  { id: 'step-1-step-2', source: 'step-1', target: 'step-2' }
];

export function applyWorkflowNodeDrag(nodes: WorkflowNode[], event: DragEndEvent): WorkflowNode[] {
  if (event.active.data.current?.kind !== WORKFLOW_NODE_DRAG_KIND) return nodes;
  const activeId = String(event.active.id);
  return nodes.map((node) => node.id === activeId
    ? {
        ...node,
        position: {
          x: node.position.x + event.delta.x,
          y: node.position.y + event.delta.y
        }
      }
    : node);
}

function WorkflowStepNode({ id, data }: NodeProps<WorkflowNode>) {
  const { attributes, isDragging, listeners, setNodeRef, transform } = useDraggable({
    id,
    data: { kind: WORKFLOW_NODE_DRAG_KIND }
  });
  const style = transform
    ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)` }
    : undefined;

  return (
    <div ref={setNodeRef} className={`min-w-44 rounded-md border bg-card ${isDragging ? 'z-2 border-brand-600' : 'border-border'}`} style={style}>
      <Handle type="target" position={Position.Left} />
      <button
        type="button"
        className="flex w-full cursor-grab flex-col items-start gap-1 border-0 bg-transparent p-4 text-left text-foreground active:cursor-grabbing"
        aria-label={`Move ${data.label}`}
        {...attributes}
        {...listeners}
      >
        <span>{data.label}</span>
        <small>Drag to reposition</small>
      </button>
      <Handle type="source" position={Position.Right} />
    </div>
  );
}

const nodeTypes = { workflowStep: WorkflowStepNode } satisfies NodeTypes;

function WorkflowCanvasContent() {
  const [nodes, setNodes, onNodesChange] = useNodesState<WorkflowNode>(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>(initialEdges);
  const nextNodeNumber = useRef(initialNodes.length + 1);

  const handleConnect = useCallback((connection: Connection) => {
    setEdges((currentEdges) => addEdge(connection, currentEdges));
  }, [setEdges]);

  const handleDragEnd = useCallback((event: DragEndEvent) => {
    // ZoomAwareDndContext has already translated this delta into flow coordinates.
    setNodes((currentNodes) => applyWorkflowNodeDrag(currentNodes, event));
  }, [setNodes]);

  const addStep = useCallback(() => {
    const sequence = nextNodeNumber.current++;
    setNodes((currentNodes) => [
      ...currentNodes,
      {
        id: `step-${sequence}`,
        type: 'workflowStep',
        position: { x: 80 + (currentNodes.length % 3) * 220, y: 260 + Math.floor(currentNodes.length / 3) * 140 },
        data: { label: `Workflow step ${sequence}` }
      }
    ]);
  }, [setNodes]);

  return (
    <section className="min-w-0">
      <header className="mb-6 flex items-start justify-between gap-4 max-[800px]:flex-col max-[800px]:items-stretch">
        <div>
          <p className="eyebrow">Operations</p>
          <h2>Workflow canvas</h2>
          <p id="workflow-canvas-help">Drag steps to arrange them. Drag between handles to connect them.</p>
        </div>
        <button type="button" onClick={addStep}>Add step</button>
      </header>
      <div className="h-176 min-h-112 overflow-hidden rounded-md border border-border bg-background" aria-describedby="workflow-canvas-help">
        <ZoomAwareDndContext onDragEnd={handleDragEnd}>
          <ReactFlow<WorkflowNode, Edge>
            aria-label="Workflow editor"
            nodes={nodes}
            edges={edges}
            nodeTypes={nodeTypes}
            nodesDraggable={false}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={handleConnect}
            fitView
            minZoom={0.25}
            maxZoom={2}
          >
            <Background gap={20} />
            <Controls />
          </ReactFlow>
        </ZoomAwareDndContext>
      </div>
    </section>
  );
}

export function WorkflowCanvas() {
  return (
    <ReactFlowProvider>
      <WorkflowCanvasContent />
    </ReactFlowProvider>
  );
}
