import { describe, expect, it, vi } from 'vitest';
import { UndoRedoStack, type Command } from '../../src/lib/commands/UndoRedoStack';
const command = (undo = vi.fn().mockResolvedValue(undefined)): Command => ({ id: '1', description: 'edit', timestamp: 1, execute: vi.fn().mockResolvedValue(undefined), undo });
describe('UndoRedoStack', () => {
  it('executes, undoes, and redoes through the same command', async () => { const stack = new UndoRedoStack(); const item = command(); await stack.pushAndExecute(item); expect(item.execute).toHaveBeenCalledOnce(); expect(await stack.undo()).toBe(true); expect(item.undo).toHaveBeenCalledOnce(); expect(await stack.redo()).toBe(true); expect(item.execute).toHaveBeenCalledTimes(2); });
  it('discards a conflicting undo', async () => { const stack = new UndoRedoStack(); await stack.pushAndExecute(command(vi.fn().mockRejectedValue(new Error('409')))); expect(await stack.undo()).toBe(false); expect(stack.canUndo()).toBe(false); });
});
