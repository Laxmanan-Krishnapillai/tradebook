import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { msalInstance } from './msalInstance';
import { apiScopes } from './msalConfig';

export type ApiTokenResult = { kind: 'success'; accessToken: string } | { kind: 'interaction-required' };

export const tokenProvider = {
  async acquireForApi(forceRefresh = false): Promise<ApiTokenResult> {
    const account = msalInstance.getActiveAccount();
    if (!account && import.meta.env.MODE === 'test') return { kind: 'success', accessToken: 'fake-test-access-token' };
    if (!account) return { kind: 'interaction-required' };
    try {
      const result = await msalInstance.acquireTokenSilent({ account, scopes: [...apiScopes], forceRefresh });
      return { kind: 'success', accessToken: result.accessToken };
    } catch (error) {
      if (error instanceof InteractionRequiredAuthError) return { kind: 'interaction-required' };
      throw error;
    }
  },
};
