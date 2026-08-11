import {
  AssistantRuntimeProvider,
  ComposerPrimitive,
  MessagePartPrimitive,
  MessagePrimitive,
  ThreadPrimitive,
  useAuiState,
  type AssistantRuntime,
} from '@assistant-ui/react';
import { Button } from './button';

function MessageText() {
  return <MessagePartPrimitive.Text component="p" smooth className="whitespace-pre-wrap" />;
}

function ThreadMessage() {
  const role = useAuiState((state) => state.message.role);
  const isUser = role === 'user';

  return (
    <MessagePrimitive.Root
      className={`flex max-w-3xl flex-col gap-1 rounded-lg border border-border p-4 ${
        isUser ? 'ml-auto bg-muted' : 'mr-auto bg-background'
      }`}
    >
      <span className="text-xs font-semibold text-muted-foreground">
        {isUser ? 'You' : 'Tradebook assistant'}
      </span>
      <div className="text-sm text-foreground">
        <MessagePrimitive.Parts components={{ Text: MessageText }} />
      </div>
    </MessagePrimitive.Root>
  );
}

export function AssistantThread({ runtime }: { runtime: AssistantRuntime }) {
  return (
    <AssistantRuntimeProvider runtime={runtime}>
      <ThreadPrimitive.Root className="flex min-h-112 flex-col overflow-hidden rounded-lg border border-border bg-background">
        <ThreadPrimitive.Viewport className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto p-4">
          <ThreadPrimitive.Empty>
            <div className="m-auto grid max-w-xl justify-items-center gap-2 p-8 text-center">
              <h2 className="text-lg font-semibold text-foreground">Ask about your trading data</h2>
              <p className="text-sm text-muted-foreground">
                This first slice is read-only. It can answer analytics questions but cannot change records.
              </p>
            </div>
          </ThreadPrimitive.Empty>
          <ThreadPrimitive.Messages components={{ Message: ThreadMessage }} />
        </ThreadPrimitive.Viewport>
        <ComposerPrimitive.Root className="flex items-end gap-3 border-t border-border p-4">
          <ComposerPrimitive.Input
            aria-label="Ask the Tradebook assistant"
            className="min-h-12 flex-1 resize-none rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground"
            placeholder="Ask an analytics question…"
            submitMode="enter"
          />
          <ComposerPrimitive.Send render={<Button intent="primary" size="sm" type="submit" />}>
            Ask
          </ComposerPrimitive.Send>
        </ComposerPrimitive.Root>
      </ThreadPrimitive.Root>
    </AssistantRuntimeProvider>
  );
}
