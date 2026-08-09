import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { Controller, useForm } from 'react-hook-form';
import { z } from 'zod';
import type { LoginRequest } from '../../api/generated/login-request';
import { ApiError, apiFetch } from '../../lib/api/client';
import { beginSession } from '../../lib/session/sessionController';
import { applyProblemDetails } from '../../lib/validation/problem-details';
import { Button } from '../ui/button';
import { Form } from '../ui/form';

const loginSchema = z.object({
  username: z.string().trim().min(1, 'Enter your username.'),
  password: z.string().min(1, 'Enter your password.'),
}) satisfies z.ZodType<LoginRequest>;

const loginResponseSchema = z.object({
  accessToken: z.string().min(1),
  expiresAtUtc: z.iso.datetime({ offset: true }),
  actorId: z.string().min(1),
});

type LoginValues = z.infer<typeof loginSchema>;

export function LoginForm({ returnPath }: { returnPath?: string }) {
  const form = useForm<LoginValues>({
    resolver: standardSchemaResolver(loginSchema),
    defaultValues: { username: '', password: '' },
  });

  const submit = form.handleSubmit(async (credentials) => {
    form.clearErrors();
    try {
      const response = await apiFetch('/api/v1/auth/login', {
        method: 'POST',
        body: JSON.stringify(credentials),
      }, loginResponseSchema);
      await beginSession(response, returnPath);
    } catch (failure) {
      if (applyProblemDetails(failure, form.setError)) return;
      form.setError('root', {
        type: 'server',
        message: failure instanceof ApiError && failure.status === 401
          ? 'Invalid username or password.'
          : 'Unable to sign in.',
      });
    }
  });

  return (
    <main className="login-shell">
      <Form {...form}>
        <form className="login-card" onSubmit={(event) => void submit(event)} noValidate>
          <p className="eyebrow">BioGem Tradebook</p>
          <h1>Sign in</h1>
          <Controller control={form.control} name="username" render={({ field, fieldState }) => (
            <label>Username<input autoComplete="username" aria-invalid={fieldState.invalid} {...field} />{fieldState.error && <span role="alert">{fieldState.error.message}</span>}</label>
          )} />
          <Controller control={form.control} name="password" render={({ field, fieldState }) => (
            <label>Password<input autoComplete="current-password" type="password" aria-invalid={fieldState.invalid} {...field} />{fieldState.error && <span role="alert">{fieldState.error.message}</span>}</label>
          )} />
          {form.formState.errors.root && <p role="alert">{form.formState.errors.root.message}</p>}
          <Button type="submit" disabled={form.formState.isSubmitting}>{form.formState.isSubmitting ? 'Signing in…' : 'Sign in'}</Button>
        </form>
      </Form>
    </main>
  );
}
