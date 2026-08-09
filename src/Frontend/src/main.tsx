import '@total-typescript/ts-reset';
import { QueryClientProvider } from '@tanstack/react-query';
import { createRoot } from 'react-dom/client';
import { MsalProvider } from '@azure/msal-react';
import { App } from './App';
import { MotionProvider } from './components/providers/motion-provider';
import { queryClient } from './lib/query/queryClient';
import './styles.css';
import { initializeMsal, msalInstance } from './lib/auth/msalInstance';

async function render(): Promise<void> {
  await initializeMsal();
  createRoot(document.getElementById('root')!).render(
    <MsalProvider instance={msalInstance}>
      <QueryClientProvider client={queryClient}>
        <MotionProvider><App /></MotionProvider>
      </QueryClientProvider>
    </MsalProvider>,
  );
}
void render();
