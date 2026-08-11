import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAgUiRuntime } from '@assistant-ui/react-ag-ui';
import { EmptyState } from '../../components/ui/empty-state';
import { AssistantThread } from '../../components/ui/assistant-thread';
import { createInAppAgent, inAppAgentStatusOptions } from '../../lib/agent/agentClient';

function EnabledAgent({ runPath }: { runPath: string }) {
  const [error, setError] = useState<string>();
  const agent = useMemo(() => createInAppAgent(runPath), [runPath]);
  const runtime = useAgUiRuntime({
    agent,
    showThinking: false,
    onError: (nextError) => setError(nextError.message),
  });

  return (
    <>
      {error ? <p role="alert">{error}</p> : null}
      <AssistantThread runtime={runtime} />
    </>
  );
}

export function InAppAgentPage() {
  const status = useQuery(inAppAgentStatusOptions);

  if (status.isPending) return <p role="status">Checking assistant availability...</p>;
  if (status.isError) {
    return <EmptyState title="Assistant unavailable" description="Tradebook could not load the assistant configuration." />;
  }

  return (
    <section className="min-w-0">
      <header className="mb-6 flex items-start justify-between gap-4">
        <div>
          <p className="eyebrow">AI workspace</p>
          <h1>Assistant</h1>
          <p>Ask authenticated, read-only questions over Tradebook analytics.</p>
        </div>
        <span className="rounded-full border border-border bg-muted px-3 py-1 text-xs font-semibold text-muted-foreground">
          Read-only
        </span>
      </header>

      {status.data.enabled
        ? <EnabledAgent runPath={status.data.runPath} />
        : <EmptyState title="Assistant not enabled" description="An administrator can enable the in-app agent after configuring its model deployment." />}
    </section>
  );
}
