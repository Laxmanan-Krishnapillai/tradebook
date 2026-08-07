import type { JsonQueryAst } from '../../types/semanticAst';
import type { ChartWidgetConfig, VisualEncodingSpec } from '../../types/visualizations';
import type { SemanticValueMembers } from '../../lib/analytics/semanticModelCatalog';

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

function selectedOptions(event: React.ChangeEvent<HTMLSelectElement>): string[] {
  return Array.from(event.currentTarget.selectedOptions, (option) => option.value);
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
    <label>
      Measures and metrics
      <select
        aria-label="Measures and metrics"
        multiple
        required
        disabled={!semanticMembers}
        value={valueColumns}
        onChange={(event) => {
          if (!semanticMembers) return;
          const values = selectedOptions(event);
          if (values.length > 0) onChange(updateValueBindings(widget, semanticMembers, values));
        }}
      >
        <optgroup label="Measures">
          {(semanticMembers?.measures ?? []).map((member) => <option key={member} value={member}>{member}</option>)}
        </optgroup>
        <optgroup label="Metrics">
          {(semanticMembers?.metrics ?? []).map((member) => <option key={member} value={member}>{member}</option>)}
        </optgroup>
      </select>
    </label>
    {!semanticMembers ? <p role="status">This semantic model has no registered query-binding catalog.</p> : null}
    <label>
      X axis
      <select
        value={widget.visualEncodings.xAxis}
        onChange={(event) => onChange({
          ...widget,
          visualEncodings: { ...widget.visualEncodings, xAxis: event.target.value }
        })}
      >
        {availableColumns.map((column) => <option key={column}>{column}</option>)}
      </select>
    </label>
    <label>
      Y axes
      <select
        multiple
        required
        value={widget.visualEncodings.yAxis}
        onChange={(event) => {
          const yAxis = selectedOptions(event);
          if (yAxis.length > 0) onChange({
            ...widget,
            visualEncodings: { ...widget.visualEncodings, yAxis }
          });
        }}
      >
        {valueColumns.map((column) => <option key={column}>{column}</option>)}
      </select>
    </label>
    <label>
      Tooltips
      <select
        multiple
        value={widget.visualEncodings.tooltipFields ?? []}
        onChange={(event) => onChange({
          ...widget,
          visualEncodings: {
            ...widget.visualEncodings,
            tooltipFields: selectedOptions(event)
          }
        })}
      >
        {availableColumns.map((column) => <option key={column}>{column}</option>)}
      </select>
    </label>
  </fieldset>;
}
