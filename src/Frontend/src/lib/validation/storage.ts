import type { z } from 'zod';

export function readValidatedStorage<T>(storage: Pick<Storage, 'getItem'>, key: string, schema: z.ZodType<T>): T | undefined {
  const serialized = storage.getItem(key);
  if (serialized === null) return undefined;
  try {
    const parsed: unknown = JSON.parse(serialized);
    const result = schema.safeParse(parsed);
    return result.success ? result.data : undefined;
  } catch {
    return undefined;
  }
}
