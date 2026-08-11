import { describe, expect, it, vi } from 'vitest';
import { UndoRedoStack, type Command } from '../../src/lib/commands/UndoRedoStack';
const command = (undo = vi.fn().mockResolvedValue(undefined)): Command => ({ id: '1', description: 'edit', timestamp: 1, execute: vi.fn().mockResolvedValue(undefined), undo });
describe('UndoRedoStack', () => {
  it('executes, undoes, and redoes through the same command', async () => { const stack = new UndoRedoStack(); const item = command(); await stack.pushAndExecute(item); expect(item.execute).toHaveBeenCalledOnce(); expect(await stack.undo()).toBe(true); expect(item.undo).toHaveBeenCalledOnce(); expect(await stack.redo()).toBe(true); expect(item.execute).toHaveBeenCalledTimes(2); });
  it('discards a conflicting undo', async () => { const stack = new UndoRedoStack(); const conflict = Object.assign(new Error('conflict'), { status: 409 }); await stack.pushAndExecute(command(vi.fn().mockRejectedValue(conflict))); expect(await stack.undo()).toBe(false); expect(stack.canUndo()).toBe(false); });

  it('keeps transiently failed undo and redo entries available for retry', async () => {
    const stack = new UndoRedoStack();
    const item = command(vi.fn().mockRejectedValueOnce(new Error('offline')).mockResolvedValue(undefined));
    await stack.pushAndExecute(item);

    expect(await stack.undo()).toBe(false);
    expect(stack.canUndo()).toBe(true);
    expect(await stack.undo()).toBe(true);

    vi.mocked(item.execute).mockRejectedValueOnce(new Error('offline'));
    await expect(stack.redo()).rejects.toThrow('offline');
    expect(stack.canRedo()).toBe(true);
    expect(await stack.redo()).toBe(true);
  });

  it('serializes overlapping edits, undo, and redo in history order', async () => {
    const stack = new UndoRedoStack();
    const events: string[] = [];
    let releaseFirst!: () => void;
    const firstGate = new Promise<void>((resolve) => { releaseFirst = resolve; });
    const first: Command = {
      id: 'first', description: 'first', timestamp: 1,
      execute: vi.fn(async () => { events.push('execute:first:start'); await firstGate; events.push('execute:first:end'); }),
      undo: vi.fn(async () => { events.push('undo:first'); }),
    };
    const second: Command = {
      id: 'second', description: 'second', timestamp: 2,
      execute: vi.fn(async () => { events.push('execute:second'); }),
      undo: vi.fn(async () => { events.push('undo:second'); }),
    };

    const firstRun = stack.pushAndExecute(first);
    const secondRun = stack.pushAndExecute(second);
    await vi.waitFor(() => expect(events).toEqual(['execute:first:start']));
    releaseFirst();
    await Promise.all([firstRun, secondRun]);
    expect(events).toEqual(['execute:first:start', 'execute:first:end', 'execute:second']);

    await Promise.all([stack.undo(), stack.undo()]);
    expect(events.slice(-2)).toEqual(['undo:second', 'undo:first']);
    await Promise.all([stack.redo(), stack.redo()]);
    expect(events.slice(-3)).toEqual(['execute:first:start', 'execute:first:end', 'execute:second']);
  });
});
