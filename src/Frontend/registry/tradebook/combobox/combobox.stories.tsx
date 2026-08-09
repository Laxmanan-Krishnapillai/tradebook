import type { Meta, StoryObj } from '@storybook/react-vite';
import { Combobox } from './combobox';

const meta = { component: Combobox } satisfies Meta<typeof Combobox>;
export default meta;
type Story = StoryObj<typeof meta>;
export const Default: Story = { args: { label: 'Market', options: [{ label: 'Power', value: 'power' }, { label: 'Gas', value: 'gas' }], value: 'power' } };
