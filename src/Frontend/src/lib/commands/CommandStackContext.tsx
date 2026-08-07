import { createContext, type ReactNode, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { isEditableTarget } from '../dom/keyboard';
import { type Command, UndoRedoStack } from './UndoRedoStack';

interface CommandStackSession {
  execute(command: Command): Promise<void>;
  undo(): Promise<boolean>;
  redo(): Promise<boolean>;
  canUndo: boolean;
  canRedo: boolean;
}

const CommandStackContext = createContext<CommandStackSession | undefined>(undefined);

export function CommandStackProvider({ children }: { children: ReactNode }) {
  const stack = useRef(new UndoRedoStack());
  const [revision, setRevision] = useState(0);
  const changed = useCallback(() => setRevision((value) => value + 1), []);
  const execute = useCallback(async (command: Command) => { await stack.current.pushAndExecute(command); changed(); }, [changed]);
  const undo = useCallback(async () => { const applied = await stack.current.undo(); changed(); return applied; }, [changed]);
  const redo = useCallback(async () => { const applied = await stack.current.redo(); changed(); return applied; }, [changed]);

  useEffect(() => {
    const listener = (event: KeyboardEvent) => {
      if (!(event.metaKey || event.ctrlKey) || event.altKey || event.key.toLowerCase() !== 'z' || isEditableTarget(event.target)) return;
      event.preventDefault();
      void (event.shiftKey ? redo() : undo()).catch(() => undefined);
    };
    document.addEventListener('keydown', listener);
    return () => document.removeEventListener('keydown', listener);
  }, [redo, undo]);

  const value = useMemo<CommandStackSession>(() => ({
    execute,
    undo,
    redo,
    canUndo: stack.current.canUndo(),
    canRedo: stack.current.canRedo()
  // revision deliberately refreshes the derived canUndo/canRedo flags.
  }), [execute, redo, revision, undo]);
  return <CommandStackContext.Provider value={value}>{children}</CommandStackContext.Provider>;
}

export function useCommandStack(): CommandStackSession {
  const value = useContext(CommandStackContext);
  if (!value) throw new Error('useCommandStack must be used inside CommandStackProvider.');
  return value;
}
