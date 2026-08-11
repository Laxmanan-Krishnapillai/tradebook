import type { JsonQueryAst } from '../../types/semanticAst';
import type { ChartWidgetConfig, VisualEncodingSpec } from '../../types/visualizations';
import type { SemanticValueMembers } from '../../lib/analytics/semanticModelCatalog';
import { MultiSelect, Select } from '../ui/select';

interface QueryBindingConfiguratorProps {
  widget: ChartWidgetConfig;
  semanticMembers?: SemanticValueMembers;
  onChange: (widget: ChartWidgetConfig) => void;
}

function queryResultColumns(query: JsonQueryAst): string[] {
  return [...new Set([
    ...(query.dimensions ?? []),
    ...(query.timeDimensions ?? []).map((value) => `${value.dimension}_${value.granularity}`),
    ...(query.measures ?? []),
    ...(query.metrics ?? [])
  ])];
}

function selectedValues(widget: ChartWidgetConfig): string[] {
  return [...(widget.queryAst.measures ?? []), ...(widget.queryAst.metrics ?? [])];
}

function optionalBinding(binding: string | undefined, validColumns: ReadonlySet<string>): string | undefined {
  return binding && validColumns.has(binding) ? binding : undefined;
}

function updateValueBindings(
  widget: ChartWidgetConfig,
  semanticMembers: SemanticValueMembers,
  requestedValues: readonly string[]
): ChartWidgetConfig {
  const allowedMeasures = new Set(semanticMembers.measures);
  const allowedMetrics = new Set(semanticMembers.metrics);
  const values = [...new Set(requestedValues)].filter(
    (value) => allowedMeasures.has(value) || allowedMetrics.has(value)
  );
  if (values.length === 0) return widget;

  const measures = values.filter((value) => allowedMeasures.has(value));
  const metrics = values.filter((value) => allowedMetrics.has(value));
  const queryAst: JsonQueryAst = {
    ...widget.queryAst,
    measures: measures.length > 0 ? measures : undefined,
    metrics: metrics.length > 0 ? metrics : undefined
  };
  const validColumns = new Set(queryResultColumns(queryAst));
  queryAst.sorts = queryAst.sorts?.filter((sort) => validColumns.has(sort.member));

  const tooltipFields = widget.visualEncodings.tooltipFields?.filter((field) => validColumns.has(field));
  const visualEncodings: VisualEncodingSpec = {
    ...widget.visualEncodings,
    xAxis: validColumns.has(widget.visualEncodings.xAxis)
      ? widget.visualEncodings.xAxis
      : queryResultColumns(queryAst)[0],
    yAxis: values,
    colorBy: optionalBinding(widget.visualEncodings.colorBy, validColumns),
    sizeBy: optionalBinding(widget.visualEncodings.sizeBy, validColumns),
    tooltipFields: tooltipFields && tooltipFields.length > 0 ? tooltipFields : undefined
  };

  return { ...widget, queryAst, visualEncodings };
}

export function QueryBindingConfigurator({
  widget,
  semanticMembers,
  onChange
}: QueryBindingConfiguratorProps) {
  const availableColumns = queryResultColumns(widget.queryAst);
  const valueColumns = selectedValues(widget);

  return <fieldset>
    <legend>Query and visual encoding — {widget.title}</legend>
    <div data-slot="query-binding-field">
      <span>Measures and metrics</span>
      <MultiSelect
        label="Measures and metrics"
        disabled={!semanticMembers}
        options={[
          ...(semanticMembers?.measures ?? []).map((member) => ({ label: `Measure · ${member}`, value: member })),
          ...(semanticMembers?.metrics ?? []).map((member) => ({ label: `Metric · ${member}`, value: member })),
        ]}
        value={valueColumns}
        onValueChange={(values) => {
          if (!semanticMembers) return;
          if (values.length > 0) onChange(updateValueBindings(widget, semanticMembers, values));
        }}
      />
    </div>
    {!semanticMembers ? <p role="status">This semantic model has no registered query-binding catalog.</p> : null}
    <div data-slot="query-binding-field">
      <span>X axis</span>
      <Select
        label="X axis"
        options={availableColumns}
        value={widget.visualEncodings.xAxis}
        onValueChange={(xAxis) => onChange({
          ...widget,
          visualEncodings: { ...widget.visualEncodings, xAxis }
        })}
      />
    </div>
    <div data-slot="query-binding-field">
      <span>Y axes</span>
      <MultiSelect
        label="Y axes"
        options={valueColumns}
        value={widget.visualEncodings.yAxis}
        onValueChange={(yAxis) => {
          if (yAxis.length > 0) onChange({
            ...widget,
            visualEncodings: { ...widget.visualEncodings, yAxis }
          });
        }}
      />
    </div>
    <div data-slot="query-binding-field">
      <span>Tooltips</span>
      <MultiSelect
        label="Tooltips"
        options={availableColumns}
        value={widget.visualEncodings.tooltipFields ?? []}
        onValueChange={(tooltipFields) => onChange({
          ...widget,
          visualEncodings: {
            ...widget.visualEncodings,
            tooltipFields
          }
        })}
      />
    </div>
  </fieldset>;
}
