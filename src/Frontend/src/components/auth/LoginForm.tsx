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
  return <main className="login-card"><h1>Tradebook</h1><p>Sign in with your organization’s Microsoft account.</p><button type="button" disabled={inProgress !== 'none'} onClick={() => void signIn()}>{inProgress === 'none' ? 'Sign in with Microsoft' : 'Completing Microsoft sign-in…'}</button></main>;
}
