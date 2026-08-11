import { useHotkeys } from '@tanstack/react-hotkeys';
import { createContext, type ReactNode, useCallback, useContext, useRef, useState } from 'react';
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
  const [, setRevision] = useState(0);
  const changed = useCallback(() => setRevision((value) => value + 1), []);
  const execute = useCallback(async (command: Command) => { await stack.current.pushAndExecute(command); changed(); }, [changed]);
  const undo = useCallback(async () => { const applied = await stack.current.undo(); changed(); return applied; }, [changed]);
  const redo = useCallback(async () => { const applied = await stack.current.redo(); changed(); return applied; }, [changed]);

  useHotkeys([
    { hotkey: 'Mod+Z', callback: () => void undo().catch(() => undefined) },
    { hotkey: 'Mod+Shift+Z', callback: () => void redo().catch(() => undefined) },
    { hotkey: 'Mod+Y', callback: () => void redo().catch(() => undefined) },
  ], {
    ignoreInputs: true,
    preventDefault: true,
  });

  const value: CommandStackSession = {
    execute,
    undo,
    redo,
    canUndo: stack.current.canUndo(),
    canRedo: stack.current.canRedo()
  };
  return <CommandStackContext.Provider value={value}>{children}</CommandStackContext.Provider>;
}

export function useCommandStack(): CommandStackSession {
  const value = useContext(CommandStackContext);
  if (!value) throw new Error('useCommandStack must be used inside CommandStackProvider.');
  return value;
}
