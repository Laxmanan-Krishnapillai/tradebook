import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from '../src/mocks/server';
import 'vitest-axe/extend-expect';

globalThis.IS_REACT_ACT_ENVIRONMENT = true;

// Base UI dispatches a PointerEvent to preserve modifier keys when a checkbox is activated.
if (!globalThis.PointerEvent) Object.defineProperty(globalThis, 'PointerEvent', { configurable: true, value: MouseEvent });

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
