export function isEditableTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLElement &&
    (target.isContentEditable || target.matches('input, textarea, select, [contenteditable="true"]'));
}
