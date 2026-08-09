import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { z } from 'zod';
import { ApiError } from '../api/client';

const problemDetailsSchema = z.object({
  type: z.string().optional(),
  title: z.string().optional(),
  status: z.number().int().optional(),
  detail: z.string().optional(),
  errors: z.record(z.string(), z.array(z.string())).optional(),
});

export function applyProblemDetails<T extends FieldValues>(error: unknown, setError: UseFormSetError<T>): boolean {
  const candidate = error instanceof ApiError ? error.problem : error;
  const parsed = problemDetailsSchema.safeParse(candidate);
  if (!parsed.success || !parsed.data.errors) return false;
  for (const [field, messages] of Object.entries(parsed.data.errors)) {
    const path = `${field.charAt(0).toLowerCase()}${field.slice(1)}` as Path<T>;
    setError(path, { type: 'server', message: messages.join(' ') });
  }
  return true;
}
