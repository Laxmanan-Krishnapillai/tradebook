import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import { InAppAgentPage } from '../../src/features/agent/InAppAgentPage';
import { server } from '../../src/mocks/server';

beforeEach(() => {
  vi.stubGlobal('ResizeObserver', class {
    observe() { /* Layout measurement is outside this component test boundary. */ }
    unobserve() { /* Layout measurement is outside this component test boundary. */ }
    disconnect() { /* Layout measurement is outside this component test boundary. */ }
  });
});

afterEach(() => vi.unstubAllGlobals());

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><InAppAgentPage /></QueryClientProvider>);
}

describe('in-app assistant page', () => {
  it('keeps the conversation surface unavailable when the server feature is disabled', async () => {
    server.use(http.get('/api/v1/agent/status', () => HttpResponse.json({
      enabled: false,
      readOnly: true,
      transport: 'AG-UI',
      runPath: '/api/v1/agent/run',
    })));

    const view = renderPage();

    expect(await screen.findByRole('heading', { name: 'Assistant not enabled' })).toBeTruthy();
    expect(screen.queryByLabelText('Ask the Tradebook assistant')).toBeNull();
    expect((await axe(view.container, { rules: { 'color-contrast': { enabled: false } } })).violations).toEqual([]);
    view.unmount();
  });

  it('mounts the read-only AG-UI conversation surface when enabled', async () => {
    server.use(http.get('/api/v1/agent/status', () => HttpResponse.json({
      enabled: true,
      readOnly: true,
      transport: 'AG-UI',
      runPath: '/api/v1/agent/run',
    })));

    const view = renderPage();

    expect(await screen.findByLabelText('Ask the Tradebook assistant')).toBeTruthy();
    expect(screen.getByText('Read-only')).toBeTruthy();
    expect(screen.queryByText(/thinking/i)).toBeNull();
    expect((await axe(view.container, { rules: { 'color-contrast': { enabled: false } } })).violations).toEqual([]);
    view.unmount();
  });
});
