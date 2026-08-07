import { describe, expect, it, vi } from 'vitest';
import { EntityEventBatcher } from '../../src/lib/streaming/eventBatcher';
const event = (sequenceId: number) => ({ eventId: String(sequenceId), sequenceId, aggregateType: 'PhysicalDelivery', aggregateId: 'one', eventType: 'Updated', payloadJson: '{}' });
describe('EntityEventBatcher', () => {
  it('keeps the highest sequence event for each aggregate', () => { const batcher = new EntityEventBatcher(); expect(batcher.coalesceEventBatch([event(1), event(2)])).toEqual([event(2)]); });
  it('emits a coalesced RxJS buffer window', () => { vi.useFakeTimers(); const ready = vi.fn(); const batcher = new EntityEventBatcher(50); batcher.start(ready); batcher.pushEvent(event(1)); batcher.pushEvent(event(2)); vi.advanceTimersByTime(50); expect(ready).toHaveBeenCalledWith([event(2)]); batcher.stop(); vi.useRealTimers(); });
});
