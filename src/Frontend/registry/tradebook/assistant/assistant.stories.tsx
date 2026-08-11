import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { useMemo } from 'react';
import { InAppAgentPage } from '../../../src/features/agent/InAppAgentPage';
import { inAppAgentStatusOptions } from '../../../src/lib/agent/agentClient';

function AssistantStory({ enabled }: { enabled: boolean }) {
  const client = useMemo(() => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(inAppAgentStatusOptions.queryKey, {
      enabled,
      readOnly: true,
      transport: 'AG-UI',
      runPath: '/api/v1/agent/run',
    });
    return queryClient;
  }, [enabled]);

  return <div className="workspace"><QueryClientProvider client={client}><InAppAgentPage /></QueryClientProvider></div>;
}

const meta = {
  title: 'Tradebook/Assistant',
  component: AssistantStory,
  parameters: { layout: 'fullscreen' },
} satisfies Meta<typeof AssistantStory>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Enabled: Story = { args: { enabled: true } };
export const Disabled: Story = { args: { enabled: false } };
