import * as signalR from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { apiFetch } from '../api/client';
import { useAuthStore } from '../state/useAuthStore';
export interface EntityChangedEvent { eventId: string; sequenceId: number; aggregateType: string; aggregateId: string; eventType: string; payloadJson: string; }
interface CatchUpResponse { events: EntityChangedEvent[]; latestSequence: number; }
class LruSet { private readonly values = new Map<string, true>(); constructor(private readonly capacity: number) {} has(id: string): boolean { return this.values.has(id); } add(id: string): void { this.values.delete(id); this.values.set(id, true); if (this.values.size > this.capacity) this.values.delete(this.values.keys().next().value as string); } }
export class DashboardStreamClient {
  private readonly connection: signalR.HubConnection; private lastSequenceId = 0; private readonly seen = new LruSet(10_000);
  constructor(private readonly onEvent: (event: EntityChangedEvent) => void) { this.connection = new signalR.HubConnectionBuilder().withUrl('/hubs/dashboard', { accessTokenFactory: () => useAuthStore.getState().accessToken }).withHubProtocol(new MessagePackHubProtocol()).withAutomaticReconnect().build(); this.connection.on('EntityChanged', (eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson) => this.handle({ eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson })); this.connection.onreconnected(() => void this.catchUp()); }
  async start(): Promise<void> { await this.connection.start(); for (const group of ['entity:PhysicalDelivery', 'entity:Contract', 'entity:MarketPrice']) await this.connection.invoke('Subscribe', group); await this.catchUp(); }
  stop(): Promise<void> { return this.connection.stop(); }
  private handle(event: EntityChangedEvent): void { if (this.seen.has(event.eventId)) return; this.seen.add(event.eventId); this.lastSequenceId = Math.max(this.lastSequenceId, event.sequenceId); this.onEvent(event); }
  public async catchUp(): Promise<void> { let page: EntityChangedEvent[]; do { const result = await apiFetch<CatchUpResponse>(`/api/v1/events?afterSequence=${this.lastSequenceId}&limit=500`); page = result.events; page.forEach((event) => this.handle(event)); } while (page.length === 500); }
}
