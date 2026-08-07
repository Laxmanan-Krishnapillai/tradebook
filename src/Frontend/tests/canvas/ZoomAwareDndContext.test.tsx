import { describe, expect, it } from 'vitest';
import { createZoomModifier } from '../../src/components/canvas/ZoomAwareDndContext';
describe('createZoomModifier', () => { it('scales the live pointer transform by zoom', () => { const modifier = createZoomModifier(2) as unknown as (value: { transform: { x: number; y: number; scaleX: number; scaleY: number } }) => { x: number; y: number }; expect(modifier({ transform: { x: 20, y: 10, scaleX: 1, scaleY: 1 } })).toMatchObject({ x: 10, y: 5 }); }); });
