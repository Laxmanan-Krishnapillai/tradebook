import '@total-typescript/ts-reset';
import { QueryClientProvider } from '@tanstack/react-query';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { configureGeneratedApiClient } from './lib/api/client';
import { queryClient } from './lib/query/queryClient';
import './styles.css';

configureGeneratedApiClient();
createRoot(document.getElementById('root')!).render(
  <QueryClientProvider client={queryClient}>
    <App />
  </QueryClientProvider>,
);
