import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ContractsPage } from '../../src/components/contracts/ContractsPage';
import { CommandStackProvider } from '../../src/lib/commands/CommandStackContext';
import { queryKeys } from '../../src/lib/query/queryKeys';
import type { ContractDetailsDto } from '../../src/api/generated/types.gen';

function jsonResponse(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), { status, headers: { 'Content-Type': 'application/json' } });
}

const now = new Date().toISOString();
const sampleContracts: ContractDetailsDto[] = [
  {
    contractId: 'c-1',
    contractName: 'Alpha contract',
    counterpartyId: 'CP-1',
    productType: 'Gas',
    action: 'Buy',
    contractType: 'External',
    isActive: true,
    version: 3,
    createdAt: now,
    updatedAt: now,
  } as ContractDetailsDto,
  {
    contractId: 'c-2',
    contractName: 'Beta contract',
    counterpartyId: 'CP-2',
    productType: 'GoO',
    action: 'Sell',
    contractType: 'External',
    isActive: true,
    version: 1,
    createdAt: now,
    updatedAt: now,
  } as ContractDetailsDto,
];

function stubContractApi() {
  const listPage = { items: sampleContracts, totalCount: sampleContracts.length, page: 1, pageSize: 100, hasNextPage: false };
  const fetchMock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
    const method = (init?.method ?? 'GET').toUpperCase();
    const url = String(_input);
    if (method === 'GET') {
      if (url.includes('/api/v1/contracts/') && !url.includes('page=')) {
        const id = url.split('/').pop();
        const contract = sampleContracts.find((item) => item.contractId === id);
        return Promise.resolve(jsonResponse(contract ?? sampleContracts[0]));
      }
      return Promise.resolve(jsonResponse(listPage));
    }
    if (method === 'POST') {
      return Promise.resolve(jsonResponse({
        ...sampleContracts[0],
        contractId: 'c-created-1',
        version: 1,
      }, 201));
    }
    if (method === 'PUT') {
      const body = JSON.parse(String(init?.body)) as Record<string, unknown>;
      return Promise.resolve(jsonResponse({ ...body, contractId: 'c-1', version: 4 }));
    }
    if (method === 'DELETE') {
      return Promise.resolve(jsonResponse({}, 200));
    }
    return Promise.resolve(jsonResponse({}, 500));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('ContractsPage interactions', () => {
  it('supports search and displays filtered counts', async () => {
    stubContractApi();
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <CommandStackProvider>
          <ContractsPage />
        </CommandStackProvider>
      </QueryClientProvider>,
    );
    await screen.findByText('2 records');
    expect(screen.getByText('2 of 2 contracts')).toBeTruthy();
    fireEvent.change(screen.getByLabelText('Search contracts'), { target: { value: 'Beta' } });
    expect(screen.getByText('1 of 2 contracts')).toBeTruthy();
    expect(screen.getByDisplayValue('Beta contract')).toBeTruthy();
    expect(screen.queryByDisplayValue('Alpha contract')).toBeNull();
    queryClient.clear();
  });

  it('opens and closes the contract detail panel from the row', async () => {
    stubContractApi();
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <CommandStackProvider>
          <ContractsPage />
        </CommandStackProvider>
      </QueryClientProvider>,
    );
    const row = await screen.findByDisplayValue('Alpha contract').then((node) => node.closest('tr'));
    expect(row).toBeTruthy();
    fireEvent.click(row!);
    await screen.findByRole('dialog', { name: 'Alpha contract' });
    fireEvent.click(screen.getByRole('button', { name: 'Close record' }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Alpha contract' })).toBeNull());
    queryClient.clear();
  });

  it('keeps a single-cell edit inline and out of the contract detail drawer', async () => {
    stubContractApi();
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <CommandStackProvider>
          <ContractsPage />
        </CommandStackProvider>
      </QueryClientProvider>,
    );

    const row = await screen.findByDisplayValue('Alpha contract').then((node) => node.closest('tr'));
    expect(row).toBeTruthy();
    const nameEditor = within(row!).getByRole('textbox', { name: 'Contract name' });
    fireEvent.click(nameEditor);
    await waitFor(() => expect(nameEditor.hasAttribute('readonly')).toBe(false));
    expect(screen.queryByRole('dialog', { name: 'Alpha contract' })).toBeNull();

    fireEvent.click(row!);
    expect(nameEditor.hasAttribute('readonly')).toBe(true);
    expect(screen.queryByRole('dialog', { name: 'Alpha contract' })).toBeNull();

    fireEvent.click(within(row!).getByRole('combobox', { name: 'Product' }));
    const productOption = await screen.findByRole('option', { name: 'GoO+Gas' });
    fireEvent.pointerDown(productOption, { button: 0, pointerType: 'mouse' });
    fireEvent.click(productOption);
    expect(screen.queryByRole('dialog', { name: 'Alpha contract' })).toBeNull();

    fireEvent.click(row!);
    expect(await screen.findByRole('dialog', { name: 'Alpha contract' })).toBeTruthy();
    queryClient.clear();
  });

  it('uses existing mutation actions for save and deactivate', async () => {
    const fetchMock = stubContractApi();
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <CommandStackProvider>
          <ContractsPage />
        </CommandStackProvider>
      </QueryClientProvider>,
    );
    const row = await screen.findByDisplayValue('Alpha contract').then((node) => node.closest('tr'));
    expect(row).toBeTruthy();
    fireEvent.click(row!);
    await screen.findByRole('dialog', { name: 'Alpha contract' });
    const dialog = screen.getByRole('dialog', { name: 'Alpha contract' });
    const nameInput = within(dialog).getByLabelText('Contract name');
    fireEvent.change(nameInput, { target: { value: 'Alpha contract v2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([, request]) => request?.method === 'PUT')).toBe(true));
    const [putUrl, putInit] = fetchMock.mock.calls.find(([, request]) => request?.method === 'PUT')!;
    expect(String(putUrl)).toContain('/api/v1/contracts/c-1');
    const updatedBody = JSON.parse(String(putInit?.body)) as Record<string, unknown>;
    expect(updatedBody.contractName).toBe('Alpha contract v2');
    fireEvent.click(screen.getByRole('button', { name: 'Deactivate' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([, request]) => request?.method === 'DELETE')).toBe(true));
    const [deleteUrl, deleteInit] = fetchMock.mock.calls.find(([, request]) => request?.method === 'DELETE')!;
    expect(String(deleteUrl)).toContain('/api/v1/contracts/c-1');
    const deletedBody = JSON.parse(String(deleteInit?.body)) as Record<string, unknown>;
    expect(deletedBody).toMatchObject({ reason: 'Deactivated from Tradebook UI' });
    queryClient.clear();
  });

  it('preserves a dirty detail draft across newer cache refreshes', async () => {
    stubContractApi();
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <CommandStackProvider>
          <ContractsPage />
        </CommandStackProvider>
      </QueryClientProvider>,
    );
    const row = await screen.findByDisplayValue('Alpha contract').then((node) => node.closest('tr'));
    fireEvent.click(row!);
    const dialog = await screen.findByRole('dialog', { name: 'Alpha contract' });
    const name = within(dialog).getByRole('textbox', { name: 'Contract name' }) as HTMLInputElement;
    fireEvent.change(name, { target: { value: 'Local unsaved name' } });

    const key = queryKeys.contracts.list({ page: 1, pageSize: 100 });
    queryClient.setQueryData(key, {
      items: [{ ...sampleContracts[0], contractName: 'Remote name', version: 4 }, sampleContracts[1]],
      totalCount: 2, page: 1, pageSize: 100, hasNextPage: false,
    });
    await waitFor(() => expect((within(dialog).getByRole('textbox', { name: 'Contract name' }) as HTMLInputElement).value).toBe('Local unsaved name'));
    expect(screen.getByText('Review and save your changes.')).toBeTruthy();

    queryClient.setQueryData(key, {
      items: [{ ...sampleContracts[0], contractName: 'Local unsaved name', version: 5 }, sampleContracts[1]],
      totalCount: 2, page: 1, pageSize: 100, hasNextPage: false,
    });
    await screen.findByText('No unsaved changes.');
    queryClient.clear();
  });
});
