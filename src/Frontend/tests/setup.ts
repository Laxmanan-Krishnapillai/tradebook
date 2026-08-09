import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest';
import { server } from '../src/mocks/server';
import { tokenProvider } from '../src/lib/auth/tokenProvider';

globalThis.IS_REACT_ACT_ENVIRONMENT = true;

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
beforeEach(() => {
  vi.spyOn(tokenProvider, 'acquireForApi').mockResolvedValue({ kind: 'success', accessToken: 'access-token' });
});
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
