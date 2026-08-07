import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState } from 'react';
import { CommandPalette } from './components/ui/CommandPalette';

const client = new QueryClient();
export function App() { const [path, setPath] = useState('/deliveries'); return <QueryClientProvider client={client}><main><h1>{path.slice(1).replace('-', ' ')}</h1><CommandPalette onNavigate={setPath} /></main></QueryClientProvider>; }
