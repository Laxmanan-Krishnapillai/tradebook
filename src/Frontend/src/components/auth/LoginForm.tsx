import { useMsal } from '@azure/msal-react';
import { apiScopes } from '../../lib/auth/msalConfig';
import { beginSession } from '../../lib/session/sessionController';

export function LoginForm({ returnPath }: { returnPath?: string }) {
  const { instance, accounts, inProgress } = useMsal();
  const signIn = async () => {
    if (accounts.length === 1) {
      const account = accounts[0];
      instance.setActiveAccount(account);
      const claims = account.idTokenClaims as { oid?: string; tid?: string } | undefined;
      if (!claims?.oid || !claims.tid) throw new Error('Microsoft account is missing oid or tid.');
      await beginSession({ accountKey: `${account.homeAccountId}:${claims.tid}:${claims.oid}`, actorId: claims.oid, displayName: account.name }, returnPath);
      return;
    }
    await instance.loginRedirect({ scopes: [...apiScopes], prompt: accounts.length > 1 ? 'select_account' : undefined, redirectStartPage: returnPath });
  };

  return <main className="flex min-h-screen items-center justify-center p-4"><form className="grid w-full max-w-md gap-4 rounded-card border border-gray-200 bg-white p-8 shadow-xl" onSubmit={(event) => void submit(event)}><p className="mb-1 text-xs font-extrabold uppercase tracking-widest text-gray-600">BioGem Tradebook</p><h1>Sign in</h1><label>Username<input autoComplete="username" required value={credentials.username} onChange={(event) => setCredentials((value) => ({ ...value, username: event.target.value }))} /></label><label>Password<input autoComplete="current-password" required type="password" value={credentials.password} onChange={(event) => setCredentials((value) => ({ ...value, password: event.target.value }))} /></label>{error && <p role="alert">{error}</p>}<button type="submit" disabled={submitting}>{submitting ? 'Signing in…' : 'Sign in'}</button></form></main>;
}
