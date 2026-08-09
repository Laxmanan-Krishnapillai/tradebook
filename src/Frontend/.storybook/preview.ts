import type { Preview } from '@storybook/react-vite';
import { createElement } from 'react';
import '../src/styles.css';

const preview: Preview = {
  parameters: {
    a11y: { test: 'error' },
    options: { storySort: { order: ['Toolbar', 'Combobox', 'DataGrid'] } },
  },
  decorators: [(Story) => createElement('div', { className: 'font-sans' }, createElement('style', undefined, '*,*::before,*::after{animation:none!important;transition:none!important}'), createElement(Story))],
};
export default preview;
