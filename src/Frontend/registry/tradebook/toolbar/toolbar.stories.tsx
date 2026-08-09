import type { Meta, StoryObj } from '@storybook/react-vite';
import { Toolbar } from './toolbar';

const meta = { component: Toolbar, args: { label: 'Trade actions' } } satisfies Meta<typeof Toolbar>;
export default meta;
type Story = StoryObj<typeof meta>;
export const Default: Story = { args: { children: <button type="button">New trade</button> } };
