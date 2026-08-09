import '@total-typescript/ts-reset';
import { QueryClientProvider } from '@tanstack/react-query';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { MotionProvider } from './components/providers/motion-provider';
import { queryClient } from './lib/query/queryClient';
import './styles.css';
createRoot(document.getElementById('root')!).render(
  <QueryClientProvider client={queryClient}>
    <MotionProvider><App /></MotionProvider>
  </QueryClientProvider>,
);
