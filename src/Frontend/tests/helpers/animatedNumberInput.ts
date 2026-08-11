import { fireEvent, waitFor } from '@testing-library/react';

function selectEditorContents(editor: HTMLElement, collapseToEnd?: boolean) {
  const selection = window.getSelection();
  if (!selection) throw new Error('Selection API is unavailable');
  const range = document.createRange();
  range.selectNodeContents(editor);
  if (collapseToEnd !== undefined) range.collapse(collapseToEnd);
  selection.removeAllRanges();
  selection.addRange(range);
}

/** Drives the library's keyboard contract; JSDOM does not emulate plaintext-only editing. */
export async function replaceAnimatedNumber(editor: HTMLElement, value: string) {
  editor.focus();
  selectEditorContents(editor);
  fireEvent.keyDown(editor, { key: 'Backspace' });
  await waitFor(() => {
    if ((editor.textContent ?? '') !== '') throw new Error('Animated number input did not clear');
  });

  let expected = '';
  for (const character of value) {
    selectEditorContents(editor, false);
    fireEvent.keyDown(editor, { key: character });
    expected += character;
    await waitFor(() => {
      if ((editor.textContent ?? '') !== expected) throw new Error(`Expected animated number input to contain ${expected}`);
    });
  }
}
