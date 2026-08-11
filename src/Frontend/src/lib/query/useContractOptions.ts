import { useQuery } from '@tanstack/react-query';
import type { ContractDetailsDto, GetContractHistoryResponse } from '../../api/generated';
import { apiFetch } from '../api/client';
import { queryKeys } from './queryKeys';

export interface ContractOption {
  description?: string;
  label: string;
  value: string;
}

export function toContractOptions(contracts: readonly ContractDetailsDto[]): ContractOption[] {
  return contracts.map((contract) => ({
    description: `${contract.productType} · ${contract.action}`,
    label: contract.contractName,
    value: contract.contractId,
  }));
}

export function useContractOptions() {
  const query = useQuery({
    queryKey: queryKeys.contracts.list({ page: 1, pageSize: 100 }),
    queryFn: ({ signal }) =>
      apiFetch<GetContractHistoryResponse>('/api/v1/contracts?page=1&pageSize=100', { signal }),
    staleTime: 30_000,
  });

  return {
    ...query,
    options: toContractOptions(query.data?.items ?? []),
  };
}
