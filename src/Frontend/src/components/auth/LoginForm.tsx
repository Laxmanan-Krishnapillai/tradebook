import { type FormEvent, useState } from 'react';
import type { LoginRequest } from '../../api/generated/login-request';
import type { LoginResponse } from '../../api/generated/login-response';
import { ApiError, apiFetch } from '../../lib/api/client';
import { beginSession } from '../../lib/session/sessionController';

export function LoginForm({ returnPath }: { returnPath?: string }) {
  const [credentials, setCredentials] = useState<LoginRequest>({ username: '', password: '' });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');
    try {
      const response = await apiFetch<LoginResponse>('/api/v1/auth/login', { method: 'POST', body: JSON.stringify(credentials) });
      await beginSession({
        accessToken: response.accessToken,
        expiresAtUtc: response.expiresAtUtc,
        actorId: response.actorId,
      }, returnPath);
    } catch (failure) {
      setError(failure instanceof ApiError && failure.status === 401 ? 'Invalid username or password.' : 'Unable to sign in.');
    } finally {
      setSubmitting(false);
    }
  };

  return <main className="login-shell"><form className="login-card" onSubmit={(event) => void submit(event)}><p className="eyebrow">BioGem Tradebook</p><h1>Sign in</h1><label>Username<input autoComplete="username" required value={credentials.username} onChange={(event) => setCredentials((value) => ({ ...value, username: event.target.value }))} /></label><label>Password<input autoComplete="current-password" required type="password" value={credentials.password} onChange={(event) => setCredentials((value) => ({ ...value, password: event.target.value }))} /></label>{error && <p role="alert">{error}</p>}<button type="submit" disabled={submitting}>{submitting ? 'Signing in…' : 'Sign in'}</button></form></main>;
}
