import * as signalR from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { apiFetch } from '../api/client';
import { useAuthStore } from '../state/useAuthStore';
import { z } from 'zod';
export interface EntityChangedEvent { eventId: string; sequenceId: number; aggregateType: string; aggregateId: string; eventType: string; payloadJson: string; }
export interface CatchUpResponse { events: EntityChangedEvent[]; latestSequence: number; }
const entityChangedEventSchema = z.object({ eventId: z.string().min(1), sequenceId: z.number().int().nonnegative(), aggregateType: z.string().min(1), aggregateId: z.string().min(1), eventType: z.string().min(1), payloadJson: z.string() });
const catchUpResponseSchema = z.object({ events: z.array(entityChangedEventSchema), latestSequence: z.number().int().nonnegative() });
export const subscribedAggregateTypes = [
  'PhysicalDelivery',
  'Contract',
  'CapacityBooking',
  'Transfer',
  'BioticketDelivery',
  'GooCertificateTransaction',
  'MarketPrice',
  'TaxTariff',
  'Hedge',
] as const;
type SubscribedAggregateType = (typeof subscribedAggregateTypes)[number];
export type KnownAggregateType = SubscribedAggregateType | 'WorkspaceDashboard';
const dashboardSubscriptionGroups = subscribedAggregateTypes.map(
  (aggregateType) => `entity:${aggregateType}` as const,
);

export function isKnownAggregateType(value: string): value is KnownAggregateType {
  return value === 'WorkspaceDashboard' || (subscribedAggregateTypes as readonly string[]).includes(value);
}

export interface DashboardHubConnection {
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(methodName: string, ...args: unknown[]): Promise<unknown>;
  on(methodName: 'EntityChanged', handler: (eventId: string, sequenceId: number, aggregateType: string, aggregateId: string, eventType: string, payloadJson: string) => void): void;
  onreconnected(handler: () => void): void;
}

export interface DashboardStreamDependencies {
  connection?: DashboardHubConnection;
  fetchPage?: (afterSequence: number) => Promise<CatchUpResponse>;
  groups?: readonly string[];
  onError?: (error: unknown) => void;
  initialRetryDelayMs?: number;
  maximumInitialRetryDelayMs?: number;
}

class LruSet { private readonly values = new Map<string, true>(); constructor(private readonly capacity: number) {} has(id: string): boolean { return this.values.has(id); } add(id: string): void { this.values.delete(id); this.values.set(id, true); if (this.values.size > this.capacity) this.values.delete(this.values.keys().next().value as string); } }
export class DashboardStreamClient {
  private readonly connection: DashboardHubConnection;
  private readonly fetchPage: (afterSequence: number) => Promise<CatchUpResponse>;
  private readonly groups: readonly string[];
  private readonly onError: (error: unknown) => void;
  private lastSequenceId = 0;
  private readonly seen = new LruSet(10_000);
  private reconnectRecovery: Promise<void> = Promise.resolve();
  private catchUpQueue: Promise<void> = Promise.resolve();
  private liveBuffer?: EntityChangedEvent[];
  private stopped = false;
  private retryTimer?: number;
  private releaseRetry?: () => void;
  private readonly initialRetryDelayMs: number;
  private readonly maximumInitialRetryDelayMs: number;

  constructor(private readonly onEvent: (event: EntityChangedEvent) => void, dependencies: DashboardStreamDependencies = {}) {
    this.connection = dependencies.connection ?? new signalR.HubConnectionBuilder()
      .withUrl('/hubs/dashboard', { accessTokenFactory: () => useAuthStore.getState().accessToken })
      .withHubProtocol(new MessagePackHubProtocol())
      .withAutomaticReconnect()
      .build();
    this.fetchPage = dependencies.fetchPage ?? ((afterSequence) => apiFetch(`/api/v1/events?afterSequence=${afterSequence}&limit=500`, {}, catchUpResponseSchema));
    const actorId = useAuthStore.getState().actorId;
    this.groups = dependencies.groups ?? (actorId ? [...dashboardSubscriptionGroups, `dashboard:${actorId}`] : dashboardSubscriptionGroups);
    this.onError = dependencies.onError ?? (() => undefined);
    this.initialRetryDelayMs = dependencies.initialRetryDelayMs ?? 500;
    this.maximumInitialRetryDelayMs = dependencies.maximumInitialRetryDelayMs ?? 10_000;
    this.connection.on('EntityChanged', (eventId: string, sequenceId: number, aggregateType: string, aggregateId: string, eventType: string, payloadJson: string) => {
      const parsed = entityChangedEventSchema.safeParse({ eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson });
      if (parsed.success) this.receiveLive(parsed.data);
      else this.onError(parsed.error);
    });
    this.connection.onreconnected(() => {
      this.reconnectRecovery = this.reconnectRecovery.then(() => this.restoreAfterReconnect()).catch((error) => this.onError(error));
    });
  }

  async start(): Promise<void> {
    this.stopped = false;
    let retryDelay = this.initialRetryDelayMs;
    while (!this.stopped) {
      const catchUpFrom = this.lastSequenceId;
      this.liveBuffer = [];
      try {
        await this.connection.start();
        if (this.stopped) { await this.connection.stop(); return; }
        await this.subscribeAll();
        if (this.stopped) { await this.connection.stop(); return; }
        await this.catchUp(catchUpFrom);
        if (this.stopped) { await this.connection.stop(); return; }
        this.flushLiveBuffer();
        return;
      } catch (error) {
        this.liveBuffer = undefined;
        this.onError(error);
        try { await this.connection.stop(); } catch { /* retry from a clean disconnected state */ }
        if (this.stopped) return;
        await this.waitBeforeRetry(retryDelay);
        retryDelay = Math.min(this.maximumInitialRetryDelayMs, retryDelay * 2);
      }
    }
  }
  async stop(): Promise<void> {
    this.stopped = true;
    if (this.retryTimer !== undefined) window.clearTimeout(this.retryTimer);
    this.releaseRetry?.();
    this.releaseRetry = undefined;
    this.retryTimer = undefined;
    this.liveBuffer = undefined;
    await this.connection.stop();
  }
  private receiveLive(event: EntityChangedEvent): void { if (this.liveBuffer) this.liveBuffer.push(event); else this.handle(event); }
  private handle(event: EntityChangedEvent): void { if (this.seen.has(event.eventId)) return; this.seen.add(event.eventId); this.lastSequenceId = Math.max(this.lastSequenceId, event.sequenceId); this.onEvent(event); }
  private flushLiveBuffer(): void {
    const buffered = this.liveBuffer ?? [];
    this.liveBuffer = undefined;
    buffered.sort((left, right) => left.sequenceId - right.sequenceId).forEach((event) => this.handle(event));
  }
  private async subscribeAll(): Promise<void> { for (const group of this.groups) await this.connection.invoke('Subscribe', group); }
  private async restoreAfterReconnect(): Promise<void> {
    if (this.stopped) return;
    const catchUpFrom = this.lastSequenceId;
    this.liveBuffer = [];
    try { await this.subscribeAll(); await this.catchUp(catchUpFrom); }
    finally { this.flushLiveBuffer(); }
  }
  private async runCatchUp(afterSequence: number): Promise<void> {
    let cursor = afterSequence;
    let page: EntityChangedEvent[];
    do {
      const result = await this.fetchPage(cursor);
      page = result.events;
      page.forEach((event) => this.handle(event));
      if (page.length) cursor = Math.max(cursor, ...page.map((event) => event.sequenceId));
      if (page.length < 500) this.lastSequenceId = Math.max(this.lastSequenceId, result.latestSequence);
    } while (page.length === 500);
  }
  public catchUp(afterSequence = this.lastSequenceId): Promise<void> {
    const result = this.catchUpQueue.then(() => this.runCatchUp(afterSequence));
    this.catchUpQueue = result.catch(() => undefined);
    return result;
  }
  public get sequenceCursor(): number { return this.lastSequenceId; }
  private waitBeforeRetry(delayMs: number): Promise<void> {
    return new Promise((resolve) => {
      const release = () => { this.releaseRetry = undefined; this.retryTimer = undefined; resolve(); };
      this.releaseRetry = release;
      this.retryTimer = window.setTimeout(release, delayMs);
    });
  }
}
