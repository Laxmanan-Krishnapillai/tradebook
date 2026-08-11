import { describe, expect, it } from 'vitest';
import type { ContractDetailsDto } from '../../src/api/generated';
import { toContractOptions } from '../../src/lib/query/useContractOptions';

describe('toContractOptions', () => {
  it('keeps authoritative ids behind human-readable contract labels', () => {
    const contract = {
      contractId: 'contract-1',
      contractName: 'BioGem supply',
      productType: 'Biomethane',
      action: 'Buy',
    } as ContractDetailsDto;

    expect(toContractOptions([contract])).toEqual([
      {
        description: 'Biomethane · Buy',
        label: 'BioGem supply',
        value: 'contract-1',
      },
    ]);
  });
});
