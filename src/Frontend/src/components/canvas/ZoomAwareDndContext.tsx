import { DndContext, type DragEndEvent, type Modifier } from '@dnd-kit/core';
import { useViewport } from '@xyflow/react';
import { type ReactNode, useMemo } from 'react';

export function createZoomModifier(zoom: number): Modifier {
  const safeZoom = zoom > 0 ? zoom : 1;
  return ({ transform }) => ({
    ...transform,
    x: transform.x / safeZoom,
    y: transform.y / safeZoom
  });
}

export function ZoomAwareDndContext({
  children,
  onDragEnd
}: {
  children: ReactNode;
  onDragEnd: (event: DragEndEvent) => void;
}) {
  const { zoom } = useViewport();
  const modifiers = useMemo(() => [createZoomModifier(zoom)], [zoom]);
  return <DndContext modifiers={modifiers} onDragEnd={onDragEnd}>{children}</DndContext>;
}
