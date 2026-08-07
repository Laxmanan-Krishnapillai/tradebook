import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { QueryBindingConfigurator } from '../../src/components/visualizations/QueryBindingConfigurator';
import { getSemanticValueMembers } from '../../src/lib/analytics/semanticModelCatalog';
import type { ChartWidgetConfig } from '../../src/types/visualizations';

const widget: ChartWidgetConfig = {
  id: 'revenue',
  title: 'Revenue',
  chartType: 'LINE',
  semanticModelRef: 'delivery_pnl_analytics',
  queryAst: {
    modelName: 'delivery_pnl_analytics',
    dimensions: ['contract_name'],
    measures: ['revenue_eur'],
    sorts: [{ member: 'revenue_eur', direction: 'desc' }]
  },
  visualEncodings: {
    xAxis: 'contract_name',
    yAxis: ['revenue_eur'],
    sizeBy: 'revenue_eur',
    tooltipFields: ['contract_name', 'revenue_eur']
  }
};

describe('QueryBindingConfigurator', () => {
  it('partitions selected values into whitelisted measures and metrics and updates the query AST', () => {
    const onChange = vi.fn();
    render(<QueryBindingConfigurator
      widget={widget}
      semanticMembers={getSemanticValueMembers(widget.queryAst.modelName)}
      onChange={onChange}
    />);
    const picker = screen.getByRole('listbox', { name: 'Measures and metrics' });
    const options = Array.from(picker.querySelectorAll('option'));
    for (const option of options) {
      option.selected = option.value === 'volume_mwh' || option.value === 'avg_price_eur_mwh';
    }
    const injected = document.createElement('option');
    injected.value = 'not_a_semantic_member';
    injected.selected = true;
    picker.append(injected);

    fireEvent.change(picker);

    expect(onChange).toHaveBeenCalledOnce();
    const updated = onChange.mock.calls[0][0] as ChartWidgetConfig;
    expect(updated.queryAst.measures).toEqual(['volume_mwh']);
    expect(updated.queryAst.metrics).toEqual(['avg_price_eur_mwh']);
    expect(JSON.stringify(updated.queryAst)).not.toContain('not_a_semantic_member');
    expect(updated.queryAst.sorts).toEqual([]);
    expect(updated.visualEncodings).toMatchObject({
      xAxis: 'contract_name',
      yAxis: ['volume_mwh', 'avg_price_eur_mwh'],
      tooltipFields: ['contract_name']
    });
    expect(updated.visualEncodings.sizeBy).toBeUndefined();
  });

  it('disables query selection when the model has no trusted member catalog', () => {
    render(<QueryBindingConfigurator widget={widget} onChange={vi.fn()} />);

    expect((screen.getByRole('listbox', { name: 'Measures and metrics' }) as HTMLSelectElement).disabled).toBe(true);
    expect(screen.getByRole('status').textContent).toContain('no registered query-binding catalog');
  });
});
