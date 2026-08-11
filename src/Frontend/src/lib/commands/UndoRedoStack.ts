export interface Command { id: string; description: string; timestamp: number; execute(): Promise<void>; undo(): Promise<void>; }
function isConflict(error: unknown): boolean {
  return typeof error === 'object' && error !== null && 'status' in error
    && (error as { status?: unknown }).status === 409;
}
export class UndoRedoStack {
  private readonly undoStack: Command[] = []; private redoStack: Command[] = [];
  private transition = Promise.resolve();
  constructor(private readonly maxSize = 100) {}
  private serialize<T>(operation: () => Promise<T>): Promise<T> {
    const result = this.transition.then(operation, operation);
    this.transition = result.then(() => undefined, () => undefined);
    return result;
  }
  pushAndExecute(command: Command): Promise<void> { return this.serialize(async () => { await command.execute(); this.undoStack.push(command); if (this.undoStack.length > this.maxSize) this.undoStack.shift(); this.redoStack = []; }); }
  undo(): Promise<boolean> { return this.serialize(async () => { const command = this.undoStack.pop(); if (!command) return false; try { await command.undo(); this.redoStack.push(command); return true; } catch (error) { if (!isConflict(error)) this.undoStack.push(command); return false; } }); }
  redo(): Promise<boolean> { return this.serialize(async () => { const command = this.redoStack.pop(); if (!command) return false; try { await command.execute(); this.undoStack.push(command); return true; } catch (error) { this.redoStack.push(command); throw error; } }); }
  canUndo(): boolean { return this.undoStack.length > 0; } canRedo(): boolean { return this.redoStack.length > 0; }
}
