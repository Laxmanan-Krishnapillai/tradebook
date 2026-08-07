import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { PhysicalDeliveryDetailsDto } from '../api/generated/physical-delivery-details-dto';
import { DashboardStreamClient, type EntityChangedEvent } from '../lib/realtime/signalRClient';
import { EntityEventBatcher } from '../lib/streaming/eventBatcher';
export function useRealtimeDeliveryCache() { const client = useQueryClient(); useEffect(() => { const batcher = new EntityEventBatcher(); batcher.start((events) => events.forEach((event) => { if (event.aggregateType !== 'PhysicalDelivery') return; const patch = JSON.parse(event.payloadJson) as Partial<PhysicalDeliveryDetailsDto>; client.setQueryData<PhysicalDeliveryDetailsDto[]>(['deliveries'], (old = []) => old.map((row) => row.deliveryId === event.aggregateId ? { ...row, ...patch } : row)); })); const stream = new DashboardStreamClient((event: EntityChangedEvent) => batcher.pushEvent(event)); void stream.start(); return () => { batcher.stop(); void stream.stop(); }; }, [client]); }
