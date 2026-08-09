import type { Meta, StoryObj } from '@storybook/react-vite';
import { DataGrid } from './data-grid';

const meta = { component: DataGrid } satisfies Meta<typeof DataGrid>;
export default meta;
type Story = StoryObj<typeof meta>;
export const ProfitAndLoss: Story = { args: { caption: 'Positions', columns: [{ key: 'commodity', label: 'Commodity' }, { key: 'profitLoss', label: 'P/L', numeric: true }], rows: [{ id: '1', commodity: 'Power', profitLoss: '+1,240.50 ▲' }, { id: '2', commodity: 'Gas', profitLoss: '-340.10 ▼' }] } };
