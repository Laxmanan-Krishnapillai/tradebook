import { Subject, Subscription, bufferTime, filter, map } from 'rxjs';
import type { EntityChangedEvent } from '../realtime/signalRClient';
export class EntityEventBatcher {
  private readonly input$ = new Subject<EntityChangedEvent>(); private subscription?: Subscription;
  constructor(private readonly windowTimeMs = 50) {}
  start(onBatchReady: (batch: EntityChangedEvent[]) => void): void { this.subscription = this.input$.pipe(bufferTime(this.windowTimeMs), filter((batch) => batch.length > 0), map((batch) => this.coalesceEventBatch(batch))).subscribe(onBatchReady); }
  pushEvent(event: EntityChangedEvent): void { this.input$.next(event); }
  coalesceEventBatch(batch: EntityChangedEvent[]): EntityChangedEvent[] { const latest = new Map<string, EntityChangedEvent>(); for (const event of batch) { const key = `${event.aggregateType}:${event.aggregateId}`; if ((latest.get(key)?.sequenceId ?? -1) < event.sequenceId) latest.set(key, event); } return [...latest.values()]; }
  stop(): void { this.subscription?.unsubscribe(); this.subscription = undefined; }
}
