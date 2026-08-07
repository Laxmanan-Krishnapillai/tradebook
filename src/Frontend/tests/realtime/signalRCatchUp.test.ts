import { describe, expect, it, vi } from 'vitest';
import { DashboardStreamClient, type CatchUpResponse, type DashboardHubConnection, type EntityChangedEvent } from '../../src/lib/realtime/signalRClient';

const event = (sequenceId: number): EntityChangedEvent => ({ eventId: `event-${sequenceId}`, sequenceId, aggregateType: 'PhysicalDelivery', aggregateId: '11111111-1111-1111-1111-111111111111', eventType: 'Updated', payloadJson: '{}' });

class FakeConnection implements DashboardHubConnection {
  readonly invocations: Array<[string, ...unknown[]]> = [];
  starts = 0;
  onSubscribe?: () => void;
  private eventHandler?: (eventId: string, sequenceId: number, aggregateType: string, aggregateId: string, eventType: string, payloadJson: string) => void;
  private reconnectHandler?: () => void;
  constructor(private readonly startFailures = 0) { }
  async start() { this.starts += 1; if (this.starts <= this.startFailures) throw new Error('initial connection failed'); }
  async stop() { }
  async invoke(methodName: string, ...args: unknown[]) { this.invocations.push([methodName, ...args]); if (methodName === 'Subscribe') this.onSubscribe?.(); }
  on(_methodName: 'EntityChanged', handler: (eventId: string, sequenceId: number, aggregateType: string, aggregateId: string, eventType: string, payloadJson: string) => void) { this.eventHandler = handler; }
  onreconnected(handler: () => void) { this.reconnectHandler = handler; }
  emit(item: EntityChangedEvent) { this.eventHandler?.(item.eventId, item.sequenceId, item.aggregateType, item.aggregateId, item.eventType, item.payloadJson); }
  reconnect() { this.reconnectHandler?.(); }
}

describe('DashboardStreamClient catch-up', () => {
  it('pages to a short response, deduplicates overlap, and re-subscribes before reconnect catch-up', async () => {
    const pages = [
      Array.from({ length: 500 }, (_, index) => event(index + 1)),
      Array.from({ length: 500 }, (_, index) => event(index + 501)),
      [event(1001)],
      []
    ];
    const fetchPage = vi.fn(async (_afterSequence: number): Promise<CatchUpResponse> => ({ events: pages.shift() ?? [], latestSequence: 1001 }));
    const delivered = vi.fn();
    const connection = new FakeConnection();
    connection.onSubscribe = () => { connection.onSubscribe = undefined; connection.emit(event(1001)); };
    const client = new DashboardStreamClient(delivered, { connection, fetchPage, groups: ['entity:PhysicalDelivery'] });

    await client.start();

    expect(fetchPage.mock.calls.map(([after]) => after)).toEqual([0, 500, 1000]);
    expect(delivered).toHaveBeenCalledTimes(1001);
    expect(delivered.mock.calls.at(-1)?.[0].sequenceId).toBe(1001);
    expect(client.sequenceCursor).toBe(1001);
    connection.emit(event(1001));
    expect(delivered).toHaveBeenCalledTimes(1001);

    connection.reconnect();
    await vi.waitFor(() => expect(fetchPage).toHaveBeenCalledTimes(4));
    expect(connection.invocations).toEqual([['Subscribe', 'entity:PhysicalDelivery'], ['Subscribe', 'entity:PhysicalDelivery']]);
    expect(fetchPage.mock.calls[3][0]).toBe(1001);
  });

  it('retries a failed initial connection and reports the transient failure', async () => {
    vi.useFakeTimers();
    const connection = new FakeConnection(1);
    const onError = vi.fn();
    const client = new DashboardStreamClient(vi.fn(), {
      connection,
      fetchPage: async () => ({ events: [], latestSequence: 0 }),
      groups: ['entity:PhysicalDelivery'],
      onError,
      initialRetryDelayMs: 5,
      maximumInitialRetryDelayMs: 5
    });

    const starting = client.start();
    await vi.advanceTimersByTimeAsync(5);
    await starting;

    expect(connection.starts).toBe(2);
    expect(onError).toHaveBeenCalledTimes(1);
    expect(connection.invocations).toEqual([['Subscribe', 'entity:PhysicalDelivery']]);
    vi.useRealTimers();
  });
});
