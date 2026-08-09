import { z } from 'zod';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../src/lib/api/client';
import { applyProblemDetails } from '../../src/lib/validation/problem-details';
import { internalPath } from '../../src/lib/validation/return-url';
import { readValidatedStorage } from '../../src/lib/validation/storage';
import { loginSearchSchema } from '../../src/app/routes/login';

describe('external input validation', () => {
  it.each(['/deliveries', '/positions/42?tab=audit', '/dashboard#today'])(
    'accepts the internal return URL %s',
    (path) => expect(internalPath.parse(path)).toBe(path),
  );

  it.each(['https://evil.com', '//evil.com/path', 'javascript:alert(1)', '/safe\\..\\evil'])(
    'rejects the hostile return URL %s',
    (path) => expect(internalPath.safeParse(path).success).toBe(false),
  );

  it('rejects a hostile return URL at the route search boundary', () => {
    expect(loginSearchSchema.parse({ redirect: 'https://evil.com' })).toEqual({ redirect: undefined });
    expect(loginSearchSchema.parse({ redirect: '/deliveries' })).toEqual({ redirect: '/deliveries' });
  });

  it('maps RFC 9457 fields and messages to React Hook Form errors', () => {
    const setError = vi.fn();
    const error = new ApiError(422, { errors: { Username: ['Already used.'], Password: ['Too short.', 'Add a digit.'] } });
    expect(applyProblemDetails(error, setError)).toBe(true);
    expect(setError).toHaveBeenCalledWith('username', { type: 'server', message: 'Already used.' });
    expect(setError).toHaveBeenCalledWith('password', { type: 'server', message: 'Too short. Add a digit.' });
  });

  it('rejects malformed and schema-invalid storage values', () => {
    const schema = z.object({ density: z.enum(['compact', 'comfortable']) });
    expect(readValidatedStorage({ getItem: () => '{bad json' }, 'preferences', schema)).toBeUndefined();
    expect(readValidatedStorage({ getItem: () => JSON.stringify({ density: 'giant' }) }, 'preferences', schema)).toBeUndefined();
    expect(readValidatedStorage({ getItem: () => JSON.stringify({ density: 'compact' }) }, 'preferences', schema)).toEqual({ density: 'compact' });
  });
});
