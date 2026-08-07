import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { PhysicalDeliveryDetailsDto } from '../../api/generated/physical-delivery-details-dto';
import type { UpdatePhysicalDeliveryRequest } from '../../api/generated/update-physical-delivery-request';
import { ApiError, apiFetch } from '../api/client';

export interface UpdateDeliveryVariables { id: string; version: number; changes: Omit<UpdatePhysicalDeliveryRequest, 'deliveryId' | 'version'>; }
export function useUpdateDelivery(onConflict: (id: string, serverState?: PhysicalDeliveryDetailsDto) => void, onErrorToast: (error: unknown) => void = () => undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, version, changes }: UpdateDeliveryVariables) => apiFetch<PhysicalDeliveryDetailsDto>(`/api/v1/deliveries/${id}`, { method: 'PUT', body: JSON.stringify({ deliveryId: id, version, ...changes }) }),
    retry: (count, error) => !(error instanceof ApiError) && count < 3,
    onMutate: async ({ id, changes }) => { await queryClient.cancelQueries({ queryKey: ['deliveries'] }); const snapshot = queryClient.getQueryData<PhysicalDeliveryDetailsDto[]>(['deliveries']); queryClient.setQueryData<PhysicalDeliveryDetailsDto[]>(['deliveries'], (old = []) => old.map((delivery) => delivery.deliveryId === id ? { ...delivery, ...changes } : delivery)); return { snapshot }; },
    onError: (error, { id }, context) => { if (context?.snapshot) queryClient.setQueryData(['deliveries'], context.snapshot); if (error instanceof ApiError && error.status === 409) { const serverState = error.problem as PhysicalDeliveryDetailsDto | undefined; queryClient.invalidateQueries({ queryKey: ['deliveries'] }); onConflict(id, serverState); return; } onErrorToast(error); },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['deliveries'] })
  });
}
