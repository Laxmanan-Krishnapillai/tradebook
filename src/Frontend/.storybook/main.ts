import type { StorybookConfig } from '@storybook/react-vite';

const config: StorybookConfig = {
  stories: ['../registry/**/*.stories.tsx'],
  addons: ['@storybook/addon-a11y', 'storybook-addon-pseudo-states'],
  framework: { name: '@storybook/react-vite', options: {} },
  env: (current) => ({
    ...current,
    VITE_ENTRA_TENANT_ID: current.VITE_ENTRA_TENANT_ID ?? 'storybook-tenant',
    VITE_ENTRA_SPA_CLIENT_ID: current.VITE_ENTRA_SPA_CLIENT_ID ?? 'storybook-spa-client',
    VITE_ENTRA_API_CLIENT_ID: current.VITE_ENTRA_API_CLIENT_ID ?? 'storybook-api-client',
    VITE_ENTRA_REDIRECT_ORIGIN: current.VITE_ENTRA_REDIRECT_ORIGIN ?? 'http://localhost:6006'
  })
};
export default config;
