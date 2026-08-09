import { PublicClientApplication } from '@azure/msal-browser';
import { msalConfig } from './msalConfig';

export const msalInstance = new PublicClientApplication(msalConfig);

export async function initializeMsal(): Promise<void> {
  await msalInstance.initialize();
  const result = await msalInstance.handleRedirectPromise();
  if (result?.account) msalInstance.setActiveAccount(result.account);
  const accounts = msalInstance.getAllAccounts();
  if (!msalInstance.getActiveAccount() && accounts.length === 1) msalInstance.setActiveAccount(accounts[0]);
  if (accounts.length > 1 && !msalInstance.getActiveAccount()) await msalInstance.loginRedirect({ scopes: [...apiScopes], prompt: 'select_account' });
}

import { apiScopes } from './msalConfig';
