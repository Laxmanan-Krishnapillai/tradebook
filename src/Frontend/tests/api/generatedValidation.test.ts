import { describe, expect, it } from 'vitest';
import { zGetEventsSinceResponse } from '../../src/api/generated/zod.gen';

describe('generated API response validation', () => {
  it('rejects a malformed API response', () => {
    expect(() => zGetEventsSinceResponse.parse({ events: [], latestSequence: 'not-an-integer' })).toThrow();
  });
});
