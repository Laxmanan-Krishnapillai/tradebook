import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from '../src/mocks/server';

globalThis.IS_REACT_ACT_ENVIRONMENT = true;

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
