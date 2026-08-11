import { BrowserCacheLocation, type Configuration } from '@azure/msal-browser';

function required(name: string): string {
  const value = import.meta.env[name] as string | undefined;
  if (!value || value === '00000000-0000-0000-0000-000000000000') throw new Error(`${name} is required and cannot be a placeholder.`);
  return value;
}

const tenantId = required('VITE_ENTRA_TENANT_ID');
const spaClientId = required('VITE_ENTRA_SPA_CLIENT_ID');
const apiClientId = required('VITE_ENTRA_API_CLIENT_ID');
const redirectOrigin = required('VITE_ENTRA_REDIRECT_ORIGIN');
const redirect = new URL(redirectOrigin);
if (redirect.pathname !== '/' || redirect.search || redirect.hash) throw new Error('VITE_ENTRA_REDIRECT_ORIGIN must be an origin without a path.');
const normalizedRedirectOrigin = redirect.origin;

export const apiScopes = [`api://${apiClientId}/access_as_user`] as const;
export const msalConfig: Configuration = {
  auth: {
    clientId: spaClientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    redirectUri: normalizedRedirectOrigin,
    postLogoutRedirectUri: normalizedRedirectOrigin,
  },
  // msal-browser v5 refuses the redirect flow whenever cacheLocation is MemoryStorage
  // (in_mem_redirect_unavailable) and removed the storeAuthStateInCookie escape hatch,
  // so keep tokens in sessionStorage: per-tab, cleared on close, redirect-compatible.
  cache: { cacheLocation: BrowserCacheLocation.SessionStorage },
};
