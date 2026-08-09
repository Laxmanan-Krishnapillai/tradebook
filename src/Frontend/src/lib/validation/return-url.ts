import { z } from 'zod';

export const internalPath = z.string().refine(
  (value) => /^\/(?!\/)[\w./?=&%#~-]*$/.test(value) && !value.includes('\\'),
  { message: 'Only internal routes are allowed' },
);
