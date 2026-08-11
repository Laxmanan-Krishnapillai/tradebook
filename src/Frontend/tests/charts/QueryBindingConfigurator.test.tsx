import { useState } from 'react';
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
  it('partitions selected values into whitelisted measures and metrics and updates the query AST', async () => {
    const onChange = vi.fn();
    function Harness() {
      const [currentWidget, setCurrentWidget] = useState(widget);
      return <QueryBindingConfigurator
        widget={currentWidget}
        semanticMembers={getSemanticValueMembers(currentWidget.queryAst.modelName)}
        onChange={(nextWidget) => {
          onChange(nextWidget);
          setCurrentWidget(nextWidget);
        }}
      />;
    }

    render(<Harness />);
    const picker = screen.getByRole('combobox', { name: 'Measures and metrics' });
    fireEvent.click(picker);

    expect(screen.queryByRole('option', { name: 'not_a_semantic_member' })).toBeNull();
    const selectOption = (option: HTMLElement) => {
      fireEvent.pointerDown(option, { button: 0, pointerType: 'mouse' });
      fireEvent.click(option);
    };
    selectOption(await screen.findByRole('option', { name: 'Measure · volume_mwh' }));
    selectOption(screen.getByRole('option', { name: 'Metric · avg_price_eur_mwh' }));
    selectOption(screen.getByRole('option', { name: 'Measure · revenue_eur' }));

    expect(onChange).toHaveBeenCalledTimes(3);
    const updated = onChange.mock.calls.at(-1)?.[0] as ChartWidgetConfig;
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

    expect((screen.getByRole('combobox', { name: 'Measures and metrics' }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByRole('status').textContent).toContain('no registered query-binding catalog');
  });
});
