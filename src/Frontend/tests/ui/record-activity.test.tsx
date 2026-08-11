import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { RecordActivity } from '../../src/components/ui/record-activity';

afterEach(() => vi.unstubAllGlobals());

describe('RecordActivity', () => {
  it('renders every persisted audit patch instead of two timestamp placeholders', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(new Response(JSON.stringify({
      items: [
        { auditId: 'a1', operation: 'UPDATE', actorId: 'actor-1', occurredAt: '2026-01-03T10:00:00Z', changes: [
          { op: 'replace', path: '/status', value: 'Completed' },
          { op: 'replace', path: '/volume_realised_mwh', value: '42.5' },
        ] },
        { auditId: 'a2', operation: 'UPDATE', actorId: 'actor-2', occurredAt: '2026-01-02T10:00:00Z', changes: [
          { op: 'replace', path: '/comments', value: 'Checked' },
        ] },
        { auditId: 'a3', operation: 'INSERT', actorId: null, occurredAt: '2026-01-01T10:00:00Z', changes: [] },
      ],
    }), { status: 200, headers: { 'Content-Type': 'application/json' } }))));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(<QueryClientProvider client={client}><RecordActivity entityId="record-1" entityName="physical_deliveries" /></QueryClientProvider>);

    expect(await screen.findByText('Status changed to Completed')).toBeTruthy();
    expect(screen.getByText('Volume Realised Mwh changed to 42.5')).toBeTruthy();
    expect(screen.getByText('Comments changed to Checked')).toBeTruthy();
    expect(screen.getByText('Record created')).toBeTruthy();
    expect(screen.getByText(/· System$/)).toBeTruthy();
  });
});
