export interface Command { id: string; description: string; timestamp: number; execute(): Promise<void>; undo(): Promise<void>; }
export class UndoRedoStack {
  private readonly undoStack: Command[] = []; private redoStack: Command[] = [];
  constructor(private readonly maxSize = 100) {}
  async pushAndExecute(command: Command): Promise<void> { await command.execute(); this.undoStack.push(command); if (this.undoStack.length > this.maxSize) this.undoStack.shift(); this.redoStack = []; }
  async undo(): Promise<boolean> { const command = this.undoStack.pop(); if (!command) return false; try { await command.undo(); this.redoStack.push(command); return true; } catch { return false; } }
  async redo(): Promise<boolean> { const command = this.redoStack.pop(); if (!command) return false; await command.execute(); this.undoStack.push(command); return true; }
  canUndo(): boolean { return this.undoStack.length > 0; } canRedo(): boolean { return this.redoStack.length > 0; }
}
