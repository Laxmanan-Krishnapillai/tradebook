import { useMsal } from '@azure/msal-react';
import { apiScopes } from '../../lib/auth/msalConfig';
import { beginSession } from '../../lib/session/sessionController';
import { PanelsTopLeft } from 'lucide-react';
import { Button } from '../ui/button';

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
  return (
    <main className="login-shell">
      <section className="login-card">
        <div data-slot="login-brand"><span data-slot="brand-mark" aria-hidden="true">B</span><strong>Tradebook</strong></div>
        <div data-slot="login-copy"><h1>Sign in to continue</h1><p>Use your BioGem Microsoft account. Sessions follow your organisation’s policy.</p></div>
        <Button intent="secondary" type="button" disabled={inProgress !== 'none'} onClick={() => void signIn()}>
          <PanelsTopLeft aria-hidden="true" size={15} />
          {inProgress === 'none' ? 'Continue with Microsoft' : 'Completing Microsoft sign-in…'}
        </Button>
        <p data-slot="login-footnote">status.tradebook.biogem · all systems normal</p>
      </section>
    </main>
  );
}
