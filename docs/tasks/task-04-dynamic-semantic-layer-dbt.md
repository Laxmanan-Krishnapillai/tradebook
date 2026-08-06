# Task 04: Dynamic Semantic Query Layer

> **DESCOPE NOTICE (2026-08-06 — applied in this revision)** — per [`architecture/decision-log.md`](../architecture/decision-log.md): DuckDB WASM + Arrow IPC are cut (**D4**); the single query path is JSON AST → C# `SemanticQueryCompiler` → parameterized SQL → JSON. Security fixes (**D11**) are folded into the body: `sorts.member`, granularity aliases, and every other identifier are validated against the compiled model whitelist before SQL assembly; YAML `sql:` fragments are trusted-admin repo content restricted to plain column identifiers. Silent-wrong-result paths are fixed: unknown filter members throw, `quarter` granularity is implemented, `contains`/`notIn` are implemented. The dbt project and marts had no consumer and are deleted. Endpoint is `POST /api/v1/analytics/query`; this file's AST is the canonical contract Task 06 consumes. The filename keeps its historical `-dbt` suffix for link stability only.

- **Phase**: Data & Analytics Layer
- **Lead / Owner**: Data Architect
- **Complexity**: Medium
- **Prerequisites**: Task 01 (PostgreSQL 17 schema & migrations), Task 02 (.NET 9 Backend Core)
- **Target Files**:
  - `src/Backend/src/Tradebook.Core/SemanticModels/delivery_pnl_analytics.yaml`
  - `src/Backend/src/Tradebook.Core/Analytics/SemanticModelDefinition.cs`
  - `src/Backend/src/Tradebook.Core/Analytics/SemanticModelLoader.cs`
  - `src/Backend/src/Tradebook.Core/Analytics/JsonQueryAst.cs`
  - `src/Backend/src/Tradebook.Core/Analytics/SemanticQueryCompiler.cs`
  - `src/Backend/src/Tradebook.Api/Endpoints/Analytics/AnalyticsQueryEndpoint.cs`
  - `src/Frontend/src/types/semanticAst.ts`
  - `tests/Tradebook.Core.Tests/Analytics/SemanticQueryCompilerTests.cs`
  - `tests/Tradebook.Api.Tests/Analytics/AnalyticsQueryEndpointTests.cs`

---

## 1. Objectives, Scope, Dependencies, Prerequisites

### 1.1 Objectives
Deliver the **Dynamic Semantic Query Layer**: a repo-versioned semantic model (entities, dimensions, measures, derived metrics, join topology) compiled server-side from a JSON query AST into parameterized SQL for PostgreSQL 17, returning JSON result sets over a single authenticated endpoint.

There is exactly **one query path** (decision-log D4):

```
React query builder → POST /api/v1/analytics/query (JSON AST)
  → C# SemanticQueryCompiler (whitelist validation, parameter binding)
  → parameterized SQL on PostgreSQL 17
  → JSON result set
```

A server round-trip on LAN (~30–80 ms) is imperceptible for human-driven pivoting at this data size; no client-side query engine exists.

### 1.2 Scope
- **Semantic model YAML** (`delivery_pnl_analytics.yaml`): repo-resident, PR-reviewed definition of entities, joins, dimensions, measures, and derived metrics.
- **JSON query AST**: the typed query contract submitted by frontend widgets and dashboard builders.
- **C# `SemanticQueryCompiler`**: validates every AST identifier against the compiled model whitelist, binds every filter value as a parameter, and emits parameterized ANSI SQL for PostgreSQL 17.
- **`POST /api/v1/analytics/query`**: FastEndpoints endpoint, JWT-authenticated (D11), JSON in / JSON out.
- **Contract ownership**: this file's AST definition and endpoint are **canonical** (decision-log ownership matrix). **Task 06 consumes this exact endpoint and AST verbatim** and defines no variant of its own.
- **Removed in this revision** (D4/D11):
  - The in-browser WASM query engine and IPC stream serialization (client engine, worker, server serializer) — see the notice above (D4).
  - The dbt project and marts (`src/Analytics/dbt_tradebook`): nothing queried them; deleting them removes an orphaned second analytics path. The filename's `-dbt` suffix is historical only.
  - Connector ingestion specs — they existed solely to feed the deleted dbt staging layer; re-specify if a real external ingestion source materializes.
  - The DB-stored, runtime-user-editable semantic-model flow. Semantic models are **repo files changed via reviewed PRs only** (trusted-admin content, D11) until a sandboxing design exists.

### 1.3 Dependencies & Technical Stack
- **Backend**: .NET 9 SDK, C# 13, FastEndpoints, Dapper + Npgsql, `YamlDotNet`.
- **Database**: plain PostgreSQL 17 (TimescaleDB is cut per D3 — no `time_bucket`, no hypertables; `date_trunc` only).
- **Frontend**: React 19, TypeScript 5.5+. Results are plain JSON — no special client packages.

### 1.4 Prerequisites
- **Task 01**: core DDL (`contracts`, `physical_deliveries`, `market_prices`, `counterparties`), bi-temporal `audit_log`, outbox.
- **Task 02**: `src/Backend/Tradebook.sln` structure, FastEndpoints configuration, DI, JWT authentication.

---

## 2. Semantic Model (repo-versioned YAML)

### 2.1 Trust & validation rules

- The YAML lives at `src/Backend/src/Tradebook.Core/SemanticModels/` and changes **only via reviewed PRs**. It is never runtime-editable and there is no `semantic_models` DB table.
- Every `sql:` value is a **plain column identifier** (`^[a-z_][a-z0-9_]*$`) that must be a member of its entity's declared `columns:` list. Arbitrary SQL expressions in YAML are **forbidden** — the loader rejects them at startup.
- JSONB extraction is structural: an optional `jsonb_key:` field (same identifier regex), compiled to `col ->> 'key'` by the compiler — never authored as raw SQL.
- Joins are structural (`left_column` / `right_column`), not free-text `ON` fragments.
- Metric expressions may reference only: measure names, numeric literals, `(` `)` `+` `-` `*` `/` `,` and `NULLIF`. The loader tokenizes and rejects anything else.

### 2.2 YAML specification (`delivery_pnl_analytics.yaml`)

```yaml
version: "1.0"
semantic_model:
  name: delivery_pnl_analytics
  description: Core semantic model for delivery revenue, cost, VAT and P&L analytics across the Tradebook books.

  # Deterministic FROM anchor: the compiler ALWAYS selects FROM this entity's
  # table. Join topology comes from the `joins` list only — never inferred from
  # the incidental order of referenced entities.
  target_entity: physical_delivery

  entities:
    - name: physical_delivery
      table: physical_deliveries
      primary_key: id
      columns: [id, contract_id, contract_instance_id, book_type, status, supply_month,
                volume_mwh, revenue_eur, tax_eur, vat_eur, invoice_amount_eur, custom_fields]
      description: Monthly delivery records for gas / GoO+Gas books (Sourcing, Sales, Intercompany).

    - name: contract
      table: contracts
      primary_key: id
      columns: [id, contract_name, product_type, counterparty_id]
      description: Master trading contracts with price mechanisms and counterparty refs.

    - name: counterparty
      table: counterparties
      primary_key: id
      columns: [id, segment]
      description: External trading partners with market segment classification.

    - name: market_price
      table: market_prices
      primary_key: price_date
      columns: [price_date]
      description: Daily TTF/EGSI ETF/THE/BGO/PGO/EUA/within-day index and EUR cross-FX time series.

  joins:
    - name: delivery_contract
      left_entity: physical_delivery
      right_entity: contract
      join_type: inner            # inner, left, right, full
      relationship: many_to_one
      left_column: contract_id    # structural join spec — no raw sql_on fragment
      right_column: id

    - name: contract_counterparty
      left_entity: contract
      right_entity: counterparty
      join_type: left
      relationship: many_to_one
      left_column: counterparty_id
      right_column: id

  dimensions:
    - name: delivery_id
      entity: physical_delivery
      type: string
      sql: id

    - name: contract_instance_id
      entity: physical_delivery
      type: string
      sql: contract_instance_id

    - name: supply_month
      entity: physical_delivery
      type: date
      sql: supply_month
      granularity: [day, week, month, quarter, year]

    - name: book_type
      entity: physical_delivery
      type: string
      sql: book_type

    - name: status
      entity: physical_delivery
      type: string
      sql: status

    - name: contract_name
      entity: contract
      type: string
      sql: contract_name

    - name: product_type
      entity: contract
      type: string
      sql: product_type

    - name: counterparty_segment
      entity: counterparty
      type: string
      sql: segment

    - name: custom_quality
      entity: physical_delivery
      type: string
      sql: custom_fields
      jsonb_key: quality          # compiled to custom_fields ->> 'quality'

  measures:
    - name: delivery_count
      entity: physical_delivery
      type: count
      sql: id

    - name: volume_mwh
      entity: physical_delivery
      type: sum
      sql: volume_mwh
      format: decimal

    - name: revenue_eur
      entity: physical_delivery
      type: sum
      sql: revenue_eur
      format: currency

    - name: tax_eur
      entity: physical_delivery
      type: sum
      sql: tax_eur
      format: currency

    - name: vat_eur
      entity: physical_delivery
      type: sum
      sql: vat_eur
      format: currency

    - name: invoice_amount_eur
      entity: physical_delivery
      type: sum
      sql: invoice_amount_eur
      format: currency

  metrics:
    - name: avg_price_eur_mwh
      description: Blended achieved price per MWh.
      expression: "revenue_eur / NULLIF(volume_mwh, 0)"
      format: currency

    - name: avg_invoice_eur_mwh
      description: Average invoiced amount per MWh.
      expression: "invoice_amount_eur / NULLIF(volume_mwh, 0)"
      format: currency

    - name: vat_ratio
      description: VAT as a ratio of invoiced amount.
      expression: "vat_eur / NULLIF(invoice_amount_eur, 0)"
      format: percentage
```

Single-tenant group: no tenant RLS predicate exists in the model. Authorization is role-claim enforcement at the API layer (D11).

### 2.3 C# schema (`SemanticModelDefinition.cs`)

```csharp
namespace Tradebook.Core.Analytics;

public sealed record SemanticModelConfig(
    string Version,
    SemanticModelRoot SemanticModel
);

public sealed record SemanticModelRoot(
    string Name,
    string Description,
    string TargetEntity,
    List<EntityDefinition> Entities,
    List<JoinDefinition> Joins,
    List<DimensionDefinition> Dimensions,
    List<MeasureDefinition> Measures,
    List<MetricDefinition> Metrics
);

public sealed record EntityDefinition(
    string Name,
    string Table,
    string PrimaryKey,
    List<string> Columns,
    string Description
);

public sealed record JoinDefinition(
    string Name,
    string LeftEntity,
    string RightEntity,
    string JoinType,        // inner | left | right | full — enum-validated by the loader
    string Relationship,
    string LeftColumn,
    string RightColumn
);

public sealed record DimensionDefinition(
    string Name,
    string Entity,
    string Type,
    string Sql,             // bare column identifier only — loader-enforced
    string? JsonbKey,       // optional ->> 'key' extraction; key regex-validated by the loader
    string? Description,
    List<string>? Granularity
);

public sealed record MeasureDefinition(
    string Name,
    string Entity,
    string Type,            // sum, avg, count, count_distinct, min, max
    string Sql,             // bare column identifier only
    string? Description,
    string? Format
);

public sealed record MetricDefinition(
    string Name,
    string Description,
    string Expression,      // measure names + numeric literals + ( ) + - * / , NULLIF only
    string? Format
);
```

### 2.4 Loader (`SemanticModelLoader.cs`) — startup, fail-fast

Registered as a DI singleton. On startup it:

1. Loads every `*.yaml` under `SemanticModels/`.
2. Validates every name, table, column, and `jsonb_key` against `^[a-z_][a-z0-9_]*$`.
3. Validates every dimension/measure `sql:` and every join column is a member of its entity's `columns:` list; `join_type` must be one of `inner|left|right|full`.
4. Tokenizes every metric expression: identifier tokens must be measure names or `NULLIF`; remaining characters limited to digits, whitespace, `( ) . + - * / ,`.
5. Verifies each non-target entity is reachable from `target_entity` via the `joins` list and precomputes, per entity, the **ordered join chain** from the target (`JoinChainFor(modelName, entityName)` → list of `(JoinDefinition Join, string NewEntity)` steps). The compiler uses these chains verbatim, so JOIN emission is deterministic.
6. Cross-checks declared `columns:` against `information_schema.columns` and **fails startup** on drift.

Any violation is a startup failure — a bad model never serves queries.

---

## 3. JSON AST & Query Compiler

### 3.1 JSON AST payload example

```json
{
  "modelName": "delivery_pnl_analytics",
  "measures": ["delivery_count", "volume_mwh", "revenue_eur"],
  "metrics": ["avg_price_eur_mwh", "vat_ratio"],
  "dimensions": ["book_type", "contract_name"],
  "timeDimensions": [
    {
      "dimension": "supply_month",
      "granularity": "quarter",
      "dateRange": ["2026-01-01T00:00:00Z", "2026-08-05T23:59:59Z"]
    }
  ],
  "filters": [
    { "member": "book_type", "operator": "equals", "values": ["Sourcing", "Sales"] },
    { "member": "revenue_eur", "operator": "greaterThan", "values": [1000.00] }
  ],
  "sorts": [
    { "member": "revenue_eur", "direction": "desc" }
  ],
  "limit": 100,
  "offset": 0
}
```

### 3.2 TypeScript AST contract (`semanticAst.ts`)

Mirrors the C# `JsonQueryAst` one-to-one. **Every operator listed here is implemented by the compiler** — no advertised-but-throwing operators (parity is asserted by tests). Task 06 imports these types; it must not declare its own.

```typescript
export type FilterOperator =
  | 'equals'
  | 'notEquals'
  | 'contains'            // ILIKE; wildcards added server-side to the bound parameter value
  | 'greaterThan'
  | 'greaterThanOrEqual'
  | 'lessThan'
  | 'lessThanOrEqual'
  | 'in'
  | 'notIn';

// Closed whitelist — anything else is rejected with HTTP 400.
export type TimeGranularity = 'day' | 'week' | 'month' | 'quarter' | 'year';

export interface TimeDimensionQuery {
  dimension: string;
  granularity: TimeGranularity;
  dateRange?: [string, string];
}

export interface FilterQuery {
  member: string;
  operator: FilterOperator;
  values: (string | number | boolean)[];
}

export interface SortQuery {
  member: string;
  direction: 'asc' | 'desc';
}

export interface JsonQueryAst {
  modelName: string;
  measures?: string[];
  metrics?: string[];
  dimensions?: string[];
  timeDimensions?: TimeDimensionQuery[];
  filters?: FilterQuery[];
  sorts?: SortQuery[];
  limit?: number;
  offset?: number;
}
```

### 3.3 C# AST types (`JsonQueryAst.cs`)

```csharp
namespace Tradebook.Core.Analytics;

public sealed record JsonQueryAst(
    string ModelName,
    List<string>? Measures,
    List<string>? Metrics,
    List<string>? Dimensions,
    List<TimeDimensionQuery>? TimeDimensions,
    List<FilterQuery>? Filters,
    List<SortQuery>? Sorts,
    int? Limit,
    int? Offset
);

public sealed record TimeDimensionQuery(string Dimension, string Granularity, string[]? DateRange);
public sealed record FilterQuery(string Member, FilterOperator Operator, List<object> Values);
public sealed record SortQuery(string Member, string Direction);

public enum FilterOperator
{
    Equals, NotEquals, Contains,
    GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual,
    In, NotIn
}
```

### 3.4 Compiler (`SemanticQueryCompiler.cs`)

Security invariants (D11), enforced structurally below:

- **Every identifier** — model name, dimensions, measures, metrics, filter members, sort members, granularities — is resolved against the compiled model whitelist. SQL text is assembled **only** from the model's own loader-validated fragments.
- **User strings never reach SQL text.** Filter values are always bound parameters; `contains` wildcards are added in C# to the parameter *value*; sort members must be selected result columns and sort direction is a two-value whitelist.
- **Unknown filter members throw** (mapped to HTTP 400 ProblemDetails). The previous behavior — silently dropping the filter and returning the full dataset — is forbidden.
- **`quarter` is implemented.** The previous switch had no `quarter` arm, so quarter queries silently returned unbucketed aggregates.
- The model's `target_entity` is **always** the FROM table; joins come from the loader's precomputed chains only (the old `requiredEntities.FirstOrDefault()`-on-a-`HashSet` FROM selection was nondeterministic).

```csharp
using System.Text;
using System.Text.RegularExpressions;

namespace Tradebook.Core.Analytics;

/// <summary>
/// Thrown for any AST element failing whitelist validation.
/// The API endpoint maps this to HTTP 400 ProblemDetails — never a silent drop.
/// </summary>
public sealed class SemanticValidationException(string message) : Exception(message);

public sealed class ParameterBag
{
    private int _counter;
    public Dictionary<string, object> Parameters { get; } = new();

    public string Bind(object value)
    {
        var name = $"@p{_counter++}";
        Parameters[name] = value;
        return name;
    }
}

public sealed class CompiledSqlQuery
{
    public required string SqlText { get; init; }
    public required Dictionary<string, object> Parameters { get; init; }
    public required List<string> ResultColumnNames { get; init; }
}

public sealed class SemanticQueryCompiler
{
    // Closed whitelist: request granularity strings map to date_trunc field
    // arguments. `quarter` is present (it previously fell through and silently
    // returned unbucketed aggregates). Anything not in this map throws.
    private static readonly Dictionary<string, string> GranularityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["day"] = "day",
        ["week"] = "week",
        ["month"] = "month",
        ["quarter"] = "quarter",
        ["year"] = "year"
    };

    private static readonly Regex IdentifierToken = new("[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

    private readonly SemanticModelRoot _model;
    private readonly SemanticModelLoader _loader; // supplies precomputed join chains

    public SemanticQueryCompiler(SemanticModelConfig config, SemanticModelLoader loader)
    {
        _model = config.SemanticModel ?? throw new ArgumentNullException(nameof(config));
        _loader = loader;
    }

    public CompiledSqlQuery Compile(JsonQueryAst ast)
    {
        ArgumentNullException.ThrowIfNull(ast);
        if (!string.Equals(ast.ModelName, _model.Name, StringComparison.Ordinal))
            throw new SemanticValidationException($"Unknown semantic model '{ast.ModelName}'.");

        var bag = new ParameterBag();
        var selectClauses = new List<string>();
        var groupByClauses = new List<string>();
        var havingClauses = new List<string>();
        var whereClauses = new List<string>();
        var resultColumns = new List<string>();
        var requiredEntities = new HashSet<string>();

        // 1. Dimensions — names validated against the model whitelist.
        foreach (var dimName in ast.Dimensions ?? [])
        {
            var dim = FindDimension(dimName);
            requiredEntities.Add(dim.Entity);
            var dimSql = DimensionSql(dim);
            selectClauses.Add($"{dimSql} AS {dim.Name}");
            groupByClauses.Add(dimSql);
            resultColumns.Add(dim.Name);
        }

        // 2. Time dimensions — granularity resolved via the closed whitelist map.
        foreach (var td in ast.TimeDimensions ?? [])
        {
            var dim = FindDimension(td.Dimension);
            requiredEntities.Add(dim.Entity);
            var dimSql = DimensionSql(dim);

            if (!GranularityMap.TryGetValue(td.Granularity, out var granularity))
                throw new SemanticValidationException(
                    $"Unknown granularity '{td.Granularity}'. Allowed: day, week, month, quarter, year.");

            // `granularity` comes from the closed map above — never from the request string.
            var timeSql = $"date_trunc('{granularity}', {dimSql})";
            var alias = $"{dim.Name}_{granularity}";
            selectClauses.Add($"{timeSql} AS {alias}");
            groupByClauses.Add(timeSql);
            resultColumns.Add(alias);

            if (td.DateRange is { Length: 2 })
            {
                var pStart = bag.Bind(DateTime.Parse(td.DateRange[0]));
                var pEnd = bag.Bind(DateTime.Parse(td.DateRange[1]));
                whereClauses.Add($"{dimSql} >= {pStart} AND {dimSql} <= {pEnd}");
            }
        }

        // 3. Measures
        foreach (var mName in ast.Measures ?? [])
        {
            var m = FindMeasure(mName);
            requiredEntities.Add(m.Entity);
            selectClauses.Add($"{BuildAggregateSql(m)} AS {m.Name}");
            resultColumns.Add(m.Name);
        }

        // 4. Derived metrics — expanded by single-pass token substitution.
        foreach (var metricName in ast.Metrics ?? [])
        {
            var metric = _model.Metrics.FirstOrDefault(x => x.Name == metricName)
                ?? throw new SemanticValidationException($"Metric '{metricName}' not found.");
            var compiled = ExpandMetricExpression(metric.Expression, requiredEntities);
            selectClauses.Add($"{compiled} AS {metric.Name}");
            resultColumns.Add(metric.Name);
        }

        if (selectClauses.Count == 0)
            throw new SemanticValidationException("Query selects no dimensions, measures or metrics.");

        // 5. Filters — an unknown member THROWS (previously it was silently
        //    dropped, returning the full unfiltered dataset). Values are bound.
        foreach (var filter in ast.Filters ?? [])
        {
            var dim = _model.Dimensions.FirstOrDefault(d => d.Name == filter.Member);
            if (dim != null)
            {
                requiredEntities.Add(dim.Entity);
                whereClauses.Add(BuildFilterClause(DimensionSql(dim), filter, bag));
                continue;
            }

            var measure = _model.Measures.FirstOrDefault(m => m.Name == filter.Member);
            if (measure != null)
            {
                requiredEntities.Add(measure.Entity);
                havingClauses.Add(BuildFilterClause(BuildAggregateSql(measure), filter, bag));
                continue;
            }

            throw new SemanticValidationException($"Unknown filter member '{filter.Member}'.");
        }

        // 6. FROM & JOINs — target_entity is ALWAYS the FROM table; joins come
        //    from the loader's precomputed chains, iterated in model-declared
        //    entity order (never HashSet order) so output is deterministic.
        var primaryEntity = _model.Entities.First(e => e.Name == _model.TargetEntity);

        var sqlBuilder = new StringBuilder();
        sqlBuilder.AppendLine("SELECT");
        sqlBuilder.AppendLine("  " + string.Join(",\n  ", selectClauses));
        sqlBuilder.AppendLine($"FROM {primaryEntity.Table}");

        var emittedJoins = new HashSet<string>();
        foreach (var entity in _model.Entities)
        {
            if (entity.Name == primaryEntity.Name || !requiredEntities.Contains(entity.Name)) continue;
            foreach (var step in _loader.JoinChainFor(_model.Name, entity.Name))
            {
                if (!emittedJoins.Add(step.Join.Name)) continue;
                var left = _model.Entities.First(e => e.Name == step.Join.LeftEntity);
                var right = _model.Entities.First(e => e.Name == step.Join.RightEntity);
                var newTable = _model.Entities.First(e => e.Name == step.NewEntity).Table;
                sqlBuilder.AppendLine(
                    $"{step.Join.JoinType.ToUpperInvariant()} JOIN {newTable} " +
                    $"ON {left.Table}.{step.Join.LeftColumn} = {right.Table}.{step.Join.RightColumn}");
            }
        }

        if (whereClauses.Count > 0)
            sqlBuilder.AppendLine("WHERE " + string.Join(" AND ", whereClauses));
        if (groupByClauses.Count > 0)
            sqlBuilder.AppendLine("GROUP BY " + string.Join(", ", groupByClauses));
        if (havingClauses.Count > 0)
            sqlBuilder.AppendLine("HAVING " + string.Join(" AND ", havingClauses));

        // 7. ORDER BY — a sort member must be one of THIS query's validated
        //    result columns; direction is a two-value whitelist. No raw user
        //    string ever reaches the ORDER BY clause.
        if (ast.Sorts is { Count: > 0 })
        {
            var sortClauses = new List<string>();
            foreach (var s in ast.Sorts)
            {
                if (!resultColumns.Contains(s.Member))
                    throw new SemanticValidationException(
                        $"Sort member '{s.Member}' is not a selected column of this query.");
                var direction = s.Direction.ToLowerInvariant() switch
                {
                    "asc" => "ASC",
                    "desc" => "DESC",
                    _ => throw new SemanticValidationException($"Invalid sort direction '{s.Direction}'.")
                };
                sortClauses.Add($"{s.Member} {direction}");
            }
            sqlBuilder.AppendLine("ORDER BY " + string.Join(", ", sortClauses));
        }

        // 8. LIMIT / OFFSET — typed ints from the AST, clamped.
        var limitVal = Math.Clamp(ast.Limit ?? 500, 1, 10_000);
        var offsetVal = Math.Max(ast.Offset ?? 0, 0);
        sqlBuilder.AppendLine($"LIMIT {limitVal} OFFSET {offsetVal}");

        return new CompiledSqlQuery
        {
            SqlText = sqlBuilder.ToString(),
            Parameters = bag.Parameters,
            ResultColumnNames = resultColumns
        };
    }

    private DimensionDefinition FindDimension(string name) =>
        _model.Dimensions.FirstOrDefault(d => d.Name == name)
            ?? throw new SemanticValidationException($"Dimension '{name}' not found in semantic model.");

    private MeasureDefinition FindMeasure(string name) =>
        _model.Measures.FirstOrDefault(m => m.Name == name)
            ?? throw new SemanticValidationException($"Measure '{name}' not found in semantic model.");

    private string DimensionSql(DimensionDefinition dim)
    {
        var table = _model.Entities.First(e => e.Name == dim.Entity).Table;
        var column = $"{table}.{dim.Sql}"; // dim.Sql is a bare, loader-validated column identifier
        return dim.JsonbKey is null ? column : $"{column} ->> '{dim.JsonbKey}'"; // key loader-validated
    }

    private string BuildAggregateSql(MeasureDefinition m)
    {
        var table = _model.Entities.First(e => e.Name == m.Entity).Table;
        var col = $"{table}.{m.Sql}"; // bare, loader-validated column identifier
        return m.Type.ToLowerInvariant() switch
        {
            "sum" => $"SUM({col})",
            "avg" => $"AVG({col})",
            "count" => $"COUNT({col})",
            "count_distinct" => $"COUNT(DISTINCT {col})",
            "min" => $"MIN({col})",
            "max" => $"MAX({col})",
            _ => throw new SemanticValidationException($"Unsupported measure aggregation type '{m.Type}'.")
        };
    }

    // Every advertised operator is implemented — TS/C# parity is a test gate.
    private static string BuildFilterClause(string targetSql, FilterQuery f, ParameterBag bag)
    {
        if (f.Values is not { Count: > 0 })
            throw new SemanticValidationException($"Filter on '{f.Member}' has no values.");

        return f.Operator switch
        {
            FilterOperator.Equals => $"{targetSql} = {bag.Bind(f.Values[0])}",
            FilterOperator.NotEquals => $"{targetSql} <> {bag.Bind(f.Values[0])}",
            FilterOperator.GreaterThan => $"{targetSql} > {bag.Bind(f.Values[0])}",
            FilterOperator.GreaterThanOrEqual => $"{targetSql} >= {bag.Bind(f.Values[0])}",
            FilterOperator.LessThan => $"{targetSql} < {bag.Bind(f.Values[0])}",
            FilterOperator.LessThanOrEqual => $"{targetSql} <= {bag.Bind(f.Values[0])}",

            // ILIKE with the wildcards added HERE, to the parameter VALUE. The SQL
            // text contains only "ILIKE @pN" — the user string itself never
            // appears in the SQL text.
            FilterOperator.Contains => $"{targetSql} ILIKE {bag.Bind($"%{f.Values[0]}%")}",

            FilterOperator.In =>
                $"{targetSql} IN ({string.Join(", ", f.Values.Select(bag.Bind))})",
            FilterOperator.NotIn =>
                $"{targetSql} NOT IN ({string.Join(", ", f.Values.Select(bag.Bind))})",

            _ => throw new SemanticValidationException($"Unsupported filter operator '{f.Operator}'.")
        };
    }

    // Single-pass token substitution — NOT string.Replace. Each identifier token
    // in the expression is resolved exactly once: measure names expand to their
    // aggregate SQL, NULLIF passes through, anything else fails validation.
    // Token matching is word-bounded by construction (the regex consumes whole
    // identifiers), so a measure named `revenue` can never corrupt
    // `revenue_eur`, and substituted SQL is never re-scanned.
    private string ExpandMetricExpression(string expression, HashSet<string> requiredEntities)
    {
        return IdentifierToken.Replace(expression, match =>
        {
            var token = match.Value;
            if (token.Equals("NULLIF", StringComparison.OrdinalIgnoreCase)) return token;

            var m = _model.Measures.FirstOrDefault(x => x.Name == token)
                ?? throw new SemanticValidationException(
                    $"Metric expression references unknown identifier '{token}'.");
            requiredEntities.Add(m.Entity);
            return BuildAggregateSql(m);
        });
    }
}
```

---

## 4. Analytics API Endpoint (`POST /api/v1/analytics/query`)

```
POST /api/v1/analytics/query
Authorization: Bearer <JWT>          (required — no AllowAnonymous, D11)
Content-Type: application/json
Body: JsonQueryAst

200: { "columns": ["book_type", "supply_month_quarter", "revenue_eur"],
       "rows": [["Sourcing", "2026-01-01T00:00:00Z", 123456.78], ...] }
400: ProblemDetails                  (any SemanticValidationException)
401: missing/invalid token
```

Endpoint responsibilities:

1. Deserialize the typed AST (`JsonQueryAst`); malformed JSON → 400.
2. Enforce JWT + role-claim authorization; actor identity comes from token claims only, never from the request body (D11).
3. Resolve the `SemanticQueryCompiler` for `modelName`; compile; map `SemanticValidationException` → 400 ProblemDetails with the validation message.
4. Execute via Dapper + Npgsql with the compiled parameter dictionary — the SQL text and parameters travel separately end to end.
5. Return `columns` + `rows` JSON.

This endpoint and AST are the contract Task 06 binds its widgets to (ownership matrix). Any divergent endpoint or AST variant in another task file is void.

---

## 5. Subagent Step-by-Step Implementation Workflow

### Step 1: Semantic model + loader
1. Create `SemanticModels/delivery_pnl_analytics.yaml` per §2.2.
2. Implement `SemanticModelDefinition.cs` (YamlDotNet deserialization) and `SemanticModelLoader.cs` with all §2.4 validations and precomputed join chains. Startup must fail on any violation.

### Step 2: AST + compiler
1. Implement `JsonQueryAst.cs` per §3.3.
2. Implement `SemanticQueryCompiler.cs` per §3.4.
3. Write the unit tests of §6.1 in `tests/Tradebook.Core.Tests`.

### Step 3: Endpoint
1. Implement `AnalyticsQueryEndpoint.cs` (FastEndpoints) per §4, JWT-protected.
2. Write the integration tests of §6.2 in `tests/Tradebook.Api.Tests`.

### Step 4: Frontend contract
1. Add `src/Frontend/src/types/semanticAst.ts` per §3.2 (or emit it via the Task 08 TypeGen pipeline if available at implementation time).

---

## 6. Comprehensive Test Plan

### 6.1 Unit tests (`tests/Tradebook.Core.Tests/Analytics/SemanticQueryCompilerTests.cs`)
- **Whitelist rejection**: unknown model name, dimension, measure, metric, filter member, sort member, granularity, and sort direction each throw `SemanticValidationException` — never a silent drop.
- **Quarter granularity**: `granularity: "quarter"` emits `date_trunc('quarter', ...)` in SELECT and GROUP BY.
- **Injection**: malicious filter *values* (e.g. `'; DROP TABLE physical_deliveries; --`) appear only as bound parameters (`@p0`...); malicious *member/granularity/sort* strings are rejected before SQL assembly. Assert the compiled `SqlText` never contains any user-supplied string.
- **`contains` / `notIn`**: `contains` compiles to `ILIKE @pN` with the parameter value wrapped in `%...%` (wildcards in the value, not the SQL text); `notIn` compiles to `NOT IN` with one bound parameter per value. Assert TS/C# operator parity (all nine operators compile).
- **Metric expansion**: with measures named `revenue` and `revenue_eur` in a test model, an expression referencing `revenue_eur` expands only the full token; an expression referencing an unknown identifier throws.
- **Deterministic FROM/JOIN**: a query selecting only `counterparty_segment` still compiles `FROM physical_deliveries` with the `delivery_contract` and `contract_counterparty` join chain, in declared order.
- **Sort whitelist**: sorting by a member that is valid in the model but not selected in this query throws.

### 6.2 Integration tests (`tests/Tradebook.Api.Tests/Analytics/AnalyticsQueryEndpointTests.cs`)
- Request without JWT → 401.
- Unknown filter member → 400 ProblemDetails carrying the validation message.
- Happy path against a seeded PostgreSQL 17 test database → 200 with matching `columns`/`rows` shape.

Functional tests green is the gate; performance is recorded as measured baselines only, per D10 — no absolute latency assertions.

---

## 7. Agent Verification Steps

```bash
# 1. Backend build + analytics tests
dotnet build src/Backend/Tradebook.sln
dotnet test tests/Tradebook.Core.Tests/Tradebook.Core.Tests.csproj --filter "FullyQualifiedName~Analytics"
dotnet test tests/Tradebook.Api.Tests/Tradebook.Api.Tests.csproj --filter "FullyQualifiedName~Analytics"

# 2. Frontend contract compiles
cd src/Frontend
npx tsc --noEmit
```

---

## 8. Anti-Cheating & Integrity Guardrails

1. **NO hardcoded SQL strings in API endpoints**: every semantic query MUST pass through `SemanticQueryCompiler`.
2. **NO hardcoded mock AST responses**: SQL must be constructed dynamically from the input AST; static pre-cooked SQL for test queries is prohibited.
3. **NO identifier interpolation**: any user-supplied string reaching compiled SQL text is an instant audit failure. Validation failures must throw (→ 400) — silently dropping an AST element is equally a failure.
4. **NO auth bypass**: `/api/v1/analytics/query` must enforce JWT + role claims on every call; no `AllowAnonymous`.
5. **NO test weakening**: deleting or inverting the rejection/injection assertions of §6.1 to make a build pass is an integrity violation.
