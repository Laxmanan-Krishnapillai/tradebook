import { DndContext, DragOverlay, type Modifier } from '@dnd-kit/core';
import { useViewport } from '@xyflow/react';
import type { ReactNode } from 'react';
export function createZoomModifier(zoom: number): Modifier { return ({ transform }) => ({ ...transform, x: transform.x / zoom, y: transform.y / zoom }); }
export function ZoomAwareDndContext({ children, onDragEnd }: { children: ReactNode; onDragEnd: () => void }) { const { zoom } = useViewport(); return <DndContext modifiers={[createZoomModifier(zoom)]} onDragEnd={onDragEnd}>{children}</DndContext>; }
export function ZoomAwareDragOverlay({ children }: { children: ReactNode }) { const { zoom } = useViewport(); return <DragOverlay dropAnimation={null}><div style={{ transform: `scale(${zoom})`, transformOrigin: 'top left' }}>{children}</div></DragOverlay>; }
