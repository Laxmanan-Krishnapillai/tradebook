# Task 04: Dynamic Semantic Query Layer & DuckDB WASM Edge Query Engine

- **Phase**: Data Pipeline & Edge Analytics Layer
- **Lead / Owner**: Data Architect / Edge Analytics Specialist
- **Complexity**: High
- **Prerequisites**: Task 01 (PostgreSQL 17 & TimescaleDB), Task 02 (.NET 9 Backend Core)
- **Target Specification File**: `tasks/task-04-dynamic-semantic-layer-dbt.md`
- **Target Target Files**:
  - `src/Backend/Tradebook.Analytics/SemanticModel/semantic_model.yaml`
  - `src/Backend/Tradebook.Analytics/SemanticModel/SemanticModelDefinition.cs`
  - `src/Backend/Tradebook.Analytics/Compiler/SemanticQueryCompiler.cs`
  - `src/Backend/Tradebook.Analytics/Ast/JsonQueryAst.cs`
  - `src/Backend/Tradebook.Analytics/Serializers/ApacheArrowStreamSerializer.cs`
  - `src/Backend/Tradebook.Analytics/Ingestion/ConnectorIngestionSpec.cs`
  - `src/Analytics/dbt_tradebook/dbt_project.yml`
  - `src/Analytics/dbt_tradebook/models/staging/stg_physical_deliveries.sql`
  - `src/Analytics/dbt_tradebook/models/marts/mart_delivery_pnl.sql`
  - `src/Analytics/dbt_tradebook/models/schema.yml`
  - `src/Frontend/src/lib/analytics/DuckDBClientEngine.ts`
  - `src/Frontend/src/lib/analytics/DuckDBWorker.ts`
  - `src/Frontend/src/types/semanticAst.ts`

---

## 1. Objectives, Scope, Dependencies, Prerequisites

### 1.1 Objectives
Task 04 delivers an enterprise-grade, high-performance **Dynamic Semantic Query Layer** coupled with an in-browser **DuckDB WASM + Apache Arrow Edge Query Engine** for Tradebook. This architecture abstracts complex relational database topologies, heterogeneous connector ingestion pipelines, and TimescaleDB time-series continuous aggregates into unified, user-defined metrics, dimensions, derived expressions, and dbt transformation models.

Combining backend parameterized SQL compilation with zero-copy Apache Arrow IPC stream serialization and client-side DuckDB WASM execution, Tradebook enables interactive data exploration (date range filtering, dynamic multi-dimension pivots, sparkline aggregation) with **sub-10ms perceived latency** on the client edge, bypassing server round-trips for repeated analytical interactions.

### 1.2 Scope
- **Semantic Model Definition (`semantic_model.yaml`)**: Standardized YAML schema for entities, dimensions, measures, derived metrics (e.g. VWAP, PnL, Win Rate), table join topologies, and contextual Row-Level Security (RLS) policies.
- **JSON Intermediate Query AST**: Strongly-typed JSON AST query representation used by frontend widgets, dashboard builders, and API consumers to query the semantic layer.
- **C# Dynamic Query Compiler (`SemanticQueryCompiler.cs`)**: High-throughput ASP.NET Core (.NET 9) compiler that parses incoming JSON AST payloads, validates metric selections against `semantic_model.yaml`, binds all filter values as parameters, constructs optimized ANSI SQL for PostgreSQL 17 / TimescaleDB, and prevents SQL injection. (Single-tenant group: access is governed by role claims at the API layer, not a `tenant_id` predicate.)
- **dbt Transformation Models & Connector Ingestion Specs**: Declarative JSON specs for ingesting heterogeneous data (REST APIs, Webhooks, SQL DBs, S3 Parquet lakes) into staging tables, paired with a production dbt project (`dbt_tradebook`) materializing analytical data marts and interfacing with TimescaleDB continuous aggregates.
- **DuckDB WASM + Apache Arrow Edge Architecture**: Server-side Apache Arrow IPC stream buffer serialization via `ApacheArrowStreamSerializer.cs` paired with a dedicated client Web Worker (`DuckDBWorker.ts` / `DuckDBClientEngine.ts`) executing DuckDB WASM in-memory queries against registered Arrow tables.

### 1.3 Dependencies & Technical Stack
- **Backend Environment**: .NET 9 SDK, C# 13, FastEndpoints, EF Core 9, Dapper, `Apache.Arrow` NuGet package (`v15.0.0+`), `YamlDotNet` (`v15.0.0+`).
- **Database Engine**: PostgreSQL 17 with TimescaleDB extension enabled.
- **Transformation Engine**: dbt-core (`v1.8.0+`) with `dbt-postgres` adapter.
- **Frontend Environment**: React 19, TypeScript 5.5+, `@duckdb/duckdb-wasm` (`v1.28.0+`), `apache-arrow` npm package (`v17.0.0+`).

### 1.4 Prerequisites
- **Task 01**: Core database DDL, `contracts` / `physical_deliveries` / `market_prices` (hypertable) plus bi-temporal audit and outbox infrastructure.
- **Task 02**: .NET 9 Solution structure (`Tradebook.sln`), FastEndpoints configuration, and dependency injection setup.

---

## 2. Dynamic YAML Semantic Model (`semantic_model.yaml`) Specification & Schema

### 2.1 Complete YAML Specification Schema

The `semantic_model.yaml` file defines the logical entities, relationship joins, dimensions, base measures, derived metrics, and security filters. It serves as the single source of truth for the dynamic query compiler.

```yaml
version: "1.0"
semantic_model:
  name: delivery_pnl_analytics
  description: Core semantic model for delivery revenue, cost, VAT and P&L analytics across the Tradebook books.

  # Single-tenant group: row-level security is scoped by authenticated role claims,
  # not by a tenant_id dimension (there is no tenants table in the entity model).
  security:
    tenant_enforcement: false
    tenant_column: null
    filter_sql: null

  entities:
    - name: physical_delivery
      table: physical_deliveries
      primary_key: id
      description: Monthly delivery records for gas / GoO+Gas books (Sourcing, Sales, Intercompany).

    - name: contract
      table: contracts
      primary_key: id
      description: Master trading contracts with price mechanisms and counterparty refs.

    - name: counterparty
      table: counterparties
      primary_key: id
      description: External trading partners with market segment classification.

    - name: market_price
      table: market_prices
      primary_key: price_date
      description: Daily TTF/EGSI ETF/THE/BGO/PGO/EUA/within-day index and EUR cross-FX time series.

  joins:
    - name: delivery_contract
      left_entity: physical_delivery
      right_entity: contract
      join_type: inner # inner, left, right, full
      relationship: many_to_one # one_to_one, one_to_many, many_to_one
      sql_on: "physical_deliveries.contract_id = contracts.id"

    - name: contract_counterparty
      left_entity: contract
      right_entity: counterparty
      join_type: left
      relationship: many_to_one
      sql_on: "contracts.counterparty_id = counterparties.id"

  dimensions:
    - name: delivery_id
      entity: physical_delivery
      type: string
      sql: physical_deliveries.id
      description: Unique identifier of the delivery record.

    - name: contract_instance_id
      entity: physical_delivery
      type: string
      sql: physical_deliveries.contract_instance_id
      description: Instance id, e.g. BFEX45.BT.2301.CO2E-9-2023.

    - name: supply_month
      entity: physical_delivery
      type: date
      sql: physical_deliveries.supply_month
      granularity: [raw, day, week, month, quarter, year]
      description: Delivery month of the physical delivery.

    - name: book_type
      entity: physical_delivery
      type: string
      sql: physical_deliveries.book_type
      description: Book classification (Sourcing, Sales, Intercompany).

    - name: status
      entity: physical_delivery
      type: string
      sql: physical_deliveries.status
      description: Invoice lifecycle status (report_status_enum).

    - name: contract_name
      entity: contract
      type: string
      sql: contracts.contract_name
      description: Canonical contract name (e.g. POST45.SH.2501.REDU).

    - name: product_type
      entity: contract
      type: string
      sql: contracts.product_type
      description: Product (GoO, Gas, GoO+Gas, GoO+Gas+Shipping, Tickets).

    - name: counterparty_segment
      entity: counterparty
      type: string
      sql: counterparties.segment
      description: Market segment of the counterparty.

    - name: custom_quality
      entity: physical_delivery
      type: string
      sql: "physical_deliveries.custom_fields ->> 'quality'"
      description: Extracted JSONB custom field quality key.

  measures:
    - name: delivery_count
      entity: physical_delivery
      type: count
      sql: physical_deliveries.id
      description: Total count of delivery records.

    - name: volume_mwh
      entity: physical_delivery
      type: sum
      sql: physical_deliveries.volume_mwh
      format: decimal
      description: Total realised volume in MWh.

    - name: revenue_eur
      entity: physical_delivery
      type: sum
      sql: physical_deliveries.revenue_eur
      format: currency
      description: Total revenue in EUR.

    - name: tax_eur
      entity: physical_delivery
      type: sum
      sql: physical_deliveries.tax_eur
      format: currency
      description: Total tax in EUR.

    - name: vat_eur
      entity: physical_delivery
      type: sum
      sql: physical_deliveries.vat_eur
      format: currency
      description: Total VAT in EUR.

    - name: invoice_amount_eur
      entity: physical_delivery
      type: sum
      sql: physical_deliveries.invoice_amount_eur
      format: currency
      description: Total invoiced amount in EUR (excl. VAT).

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

### 2.2 C# Strong Type Schema Definition (`SemanticModelDefinition.cs`)

```csharp
namespace Tradebook.Analytics.SemanticModel;

public sealed record SemanticModelConfig(
    string Version,
    SemanticModelRoot SemanticModel
);

public sealed record SemanticModelRoot(
    string Name,
    string Description,
    SecurityConfig Security,
    List<EntityDefinition> Entities,
    List<JoinDefinition> Joins,
    List<DimensionDefinition> Dimensions,
    List<MeasureDefinition> Measures,
    List<MetricDefinition> Metrics
);

public sealed record SecurityConfig(
    bool TenantEnforcement,
    string TenantColumn,
    string FilterSql
);

public sealed record EntityDefinition(
    string Name,
    string Table,
    object PrimaryKey,
    string Description
);

public sealed record JoinDefinition(
    string Name,
    string LeftEntity,
    string RightEntity,
    string JoinType,
    string Relationship,
    string SqlOn
);

public sealed record DimensionDefinition(
    string Name,
    string Entity,
    string Type,
    string Sql,
    string Description,
    List<string>? Granularity
);

public sealed record MeasureDefinition(
    string Name,
    string Entity,
    string Type, // sum, avg, count, count_distinct, min, max
    string Sql,
    string Description,
    string? Format
);

public sealed record MetricDefinition(
    string Name,
    string Description,
    string Expression,
    string? Format
);
```

---

## 3. JSON AST Intermediate Query Representation & Query Compiler

### 3.1 JSON AST Query Payload Example

Frontend visual query builders submit structured JSON AST queries to the backend analytical engine.

```json
{
  "modelName": "delivery_pnl_analytics",
  "measures": ["delivery_count", "volume_mwh", "revenue_eur"],
  "metrics": ["avg_price_eur_mwh", "vat_ratio"],
  "dimensions": ["book_type", "supply_month", "contract_name"],
  "timeDimensions": [
    {
      "dimension": "supply_month",
      "granularity": "month",
      "dateRange": ["2026-01-01T00:00:00Z", "2026-08-05T23:59:59Z"]
    }
  ],
  "filters": [
    {
      "member": "book_type",
      "operator": "equals",
      "values": ["Sourcing", "Sales"]
    },
    {
      "member": "revenue_eur",
      "operator": "greaterThan",
      "values": [1000.00]
    }
  ],
  "sorts": [
    {
      "member": "revenue_eur",
      "direction": "desc"
    }
  ],
  "limit": 100,
  "offset": 0
}
```

### 3.2 TypeScript AST Contract (`semanticAst.ts`)

```typescript
export type FilterOperator = 
  | 'equals' 
  | 'notEquals' 
  | 'contains' 
  | 'greaterThan' 
  | 'greaterThanOrEqual' 
  | 'lessThan' 
  | 'lessThanOrEqual' 
  | 'in' 
  | 'notIn';

export type TimeGranularity = 'raw' | 'minute' | 'hour' | 'day' | 'week' | 'month' | 'quarter' | 'year';

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

### 3.3 Dynamic C# AST Query Compiler (`SemanticQueryCompiler.cs`)

```csharp
using System.Text;
using Tradebook.Analytics.Ast;
using Tradebook.Analytics.SemanticModel;

namespace Tradebook.Analytics.Compiler;

public sealed class CompiledSqlQuery
{
    public required string SqlText { get; init; }
    public required Dictionary<string, object> Parameters { get; init; }
    public required List<string> ResultColumnNames { get; init; }
}

public sealed class SemanticQueryCompiler
{
    private readonly SemanticModelRoot _model;

    public SemanticQueryCompiler(SemanticModelConfig config)
    {
        _model = config.SemanticModel ?? throw new ArgumentNullException(nameof(config));
    }

    public CompiledSqlQuery Compile(JsonQueryAst ast)
    {
        ArgumentNullException.ThrowIfNull(ast);
        var parameters = new Dictionary<string, object>();
        var paramCounter = 0;

        // Single-tenant group: no tenant RLS predicate is injected here.
        // Authorization is enforced by role claims at the API endpoint layer.
        var selectClauses = new List<string>();
        var groupByClauses = new List<string>();
        var havingClauses = new List<string>();
        var whereClauses = new List<string>();

        var resultColumns = new List<string>();
        var requiredEntities = new HashSet<string>();

        // 1. Process Dimensions
        if (ast.Dimensions != null)
        {
            foreach (var dimName in ast.Dimensions)
            {
                var dim = _model.Dimensions.FirstOrDefault(d => d.Name == dimName)
                    ?? throw new InvalidOperationException($"Dimension '{dimName}' not found in semantic model.");

                requiredEntities.Add(dim.Entity);
                selectClauses.Add($"{dim.Sql} AS {dim.Name}");
                groupByClauses.Add(dim.Sql);
                resultColumns.Add(dim.Name);
            }
        }

        // 2. Process Time Dimensions
        if (ast.TimeDimensions != null)
        {
            foreach (var td in ast.TimeDimensions)
            {
                var dim = _model.Dimensions.FirstOrDefault(d => d.Name == td.Dimension)
                    ?? throw new InvalidOperationException($"Time Dimension '{td.Dimension}' not found.");

                requiredEntities.Add(dim.Entity);
                var timeSql = BuildTimeBucketSql(dim.Sql, td.Granularity);
                var alias = $"{td.Dimension}_{td.Granularity}";
                selectClauses.Add($"{timeSql} AS {alias}");
                groupByClauses.Add(timeSql);
                resultColumns.Add(alias);

                if (td.DateRange is { Length: 2 })
                {
                    var pStart = $"@p{paramCounter++}";
                    var pEnd = $"@p{paramCounter++}";
                    parameters.Add(pStart, DateTime.Parse(td.DateRange[0]));
                    parameters.Add(pEnd, DateTime.Parse(td.DateRange[1]));
                    whereClauses.Add($"{dim.Sql} >= {pStart} AND {dim.Sql} <= {pEnd}");
                }
            }
        }

        // 3. Process Base Measures
        var baseMeasuresMap = new Dictionary<string, MeasureDefinition>();
        if (ast.Measures != null)
        {
            foreach (var mName in ast.Measures)
            {
                var m = _model.Measures.FirstOrDefault(x => x.Name == mName)
                    ?? throw new InvalidOperationException($"Measure '{mName}' not found.");

                requiredEntities.Add(m.Entity);
                baseMeasuresMap[m.Name] = m;
                var measureSql = BuildAggregateSql(m);
                selectClauses.Add($"{measureSql} AS {m.Name}");
                resultColumns.Add(m.Name);
            }
        }

        // 4. Process Derived Metrics
        if (ast.Metrics != null)
        {
            foreach (var metricName in ast.Metrics)
            {
                var metric = _model.Metrics.FirstOrDefault(x => x.Name == metricName)
                    ?? throw new InvalidOperationException($"Metric '{metricName}' not found.");

                // Resolve metric dependencies into aggregate expressions
                var compiledMetricSql = ExpandMetricExpression(metric.Expression, _model.Measures, requiredEntities);
                selectClauses.Add($"{compiledMetricSql} AS {metric.Name}");
                resultColumns.Add(metric.Name);
            }
        }

        // 5. Process Filters
        if (ast.Filters != null)
        {
            foreach (var filter in ast.Filters)
            {
                var dim = _model.Dimensions.FirstOrDefault(d => d.Name == filter.Member);
                if (dim != null)
                {
                    requiredEntities.Add(dim.Entity);
                    var fSql = BuildFilterClause(dim.Sql, filter, parameters, ref paramCounter);
                    whereClauses.Add(fSql);
                }
                else
                {
                    var measure = _model.Measures.FirstOrDefault(m => m.Name == filter.Member);
                    if (measure != null)
                    {
                        requiredEntities.Add(measure.Entity);
                        var aggSql = BuildAggregateSql(measure);
                        var fSql = BuildFilterClause(aggSql, filter, parameters, ref paramCounter);
                        havingClauses.Add(fSql);
                    }
                }
            }
        }

        // 6. Build FROM & JOIN topology
        var primaryEntityName = requiredEntities.FirstOrDefault() ?? _model.Entities.First().Name;
        var primaryEntity = _model.Entities.First(e => e.Name == primaryEntityName);

        var sqlBuilder = new StringBuilder();
        sqlBuilder.AppendLine("SELECT");
        sqlBuilder.AppendLine("  " + string.Join(",\n  ", selectClauses));
        sqlBuilder.AppendLine($"FROM {primaryEntity.Table}");

        foreach (var entityName in requiredEntities.Where(e => e != primaryEntityName))
        {
            var joinDef = _model.Joins.FirstOrDefault(j =>
                (j.LeftEntity == primaryEntityName && j.RightEntity == entityName) ||
                (j.RightEntity == primaryEntityName && j.LeftEntity == entityName))
                ?? throw new InvalidOperationException($"No join path found between '{primaryEntityName}' and '{entityName}'.");

            var rightEnt = _model.Entities.First(e => e.Name == entityName);
            sqlBuilder.AppendLine($"{joinDef.JoinType.ToUpperInvariant()} JOIN {rightEnt.Table} ON {joinDef.SqlOn}");
        }

        if (whereClauses.Count > 0)
        {
            sqlBuilder.AppendLine("WHERE " + string.Join(" AND ", whereClauses));
        }

        if (groupByClauses.Count > 0)
        {
            sqlBuilder.AppendLine("GROUP BY " + string.Join(", ", groupByClauses));
        }

        if (havingClauses.Count > 0)
        {
            sqlBuilder.AppendLine("HAVING " + string.Join(" AND ", havingClauses));
        }

        // 7. Process Order By
        if (ast.Sorts is { Count: > 0 })
        {
            var sortClauses = ast.Sorts.Select(s => $"{s.Member} {s.Direction.ToUpperInvariant()}");
            sqlBuilder.AppendLine("ORDER BY " + string.Join(", ", sortClauses));
        }

        // 8. Limit & Offset
        var limitVal = ast.Limit is > 0 ? ast.Limit.Value : 500;
        var offsetVal = ast.Offset is >= 0 ? ast.Offset.Value : 0;
        sqlBuilder.AppendLine($"LIMIT {limitVal} OFFSET {offsetVal}");

        return new CompiledSqlQuery
        {
            SqlText = sqlBuilder.ToString(),
            Parameters = parameters,
            ResultColumnNames = resultColumns
        };
    }

    private static string BuildAggregateSql(MeasureDefinition m) => m.Type.ToLowerInvariant() switch
    {
        "sum" => $"SUM({m.Sql})",
        "avg" => $"AVG({m.Sql})",
        "count" => $"COUNT({m.Sql})",
        "count_distinct" => $"COUNT(DISTINCT {m.Sql})",
        "min" => $"MIN({m.Sql})",
        "max" => $"MAX({m.Sql})",
        _ => throw new InvalidOperationException($"Unsupported measure aggregation type '{m.Type}'.")
    };

    private static string BuildTimeBucketSql(string sqlColumn, string granularity) => granularity.ToLowerInvariant() switch
    {
        "minute" => $"time_bucket('1 minute', {sqlColumn})",
        "hour" => $"date_trunc('hour', {sqlColumn})",
        "day" => $"date_trunc('day', {sqlColumn})",
        "week" => $"date_trunc('week', {sqlColumn})",
        "month" => $"date_trunc('month', {sqlColumn})",
        "year" => $"date_trunc('year', {sqlColumn})",
        _ => sqlColumn
    };

    private static string BuildFilterClause(string targetSql, FilterQuery f, Dictionary<string, object> paramsDict, ref int counter)
    {
        var pName = $"@p{counter++}";
        var firstVal = f.Values.FirstOrDefault();

        switch (f.Operator)
        {
            case FilterOperator.Equals:
                paramsDict[pName] = firstVal!;
                return $"{targetSql} = {pName}";
            case FilterOperator.NotEquals:
                paramsDict[pName] = firstVal!;
                return $"{targetSql} <> {pName}";
            case FilterOperator.GreaterThan:
                paramsDict[pName] = firstVal!;
                return $"{targetSql} > {pName}";
            case FilterOperator.GreaterThanOrEqual:
                paramsDict[pName] = firstVal!;
                return $"{targetSql} >= {pName}";
            case FilterOperator.LessThan:
                paramsDict[pName] = firstVal!;
                return $"{targetSql} < {pName}";
            case FilterOperator.LessThanOrEqual:
                paramsDict[pName] = firstVal!;
                return $"{targetSql} <= {pName}";
            case FilterOperator.In:
                var inParams = new List<string>();
                foreach (var v in f.Values)
                {
                    var ipName = $"@p{counter++}";
                    paramsDict[ipName] = v;
                    inParams.Add(ipName);
                }
                return $"{targetSql} IN ({string.Join(", ", inParams)})";
            default:
                throw new NotSupportedException($"Filter operator '{f.Operator}' is not supported.");
        }
    }

    private static string ExpandMetricExpression(string expression, List<MeasureDefinition> measures, HashSet<string> requiredEntities)
    {
        var result = expression;
        foreach (var m in measures)
        {
            if (result.Contains(m.Name))
            {
                requiredEntities.Add(m.Entity);
                var aggSql = BuildAggregateSql(m);
                result = result.Replace(m.Name, aggSql);
            }
        }
        return result;
    }
}
```

---

## 4. Connector Ingestion Spec & dbt Semantic Transformation Models

### 4.1 Connector Ingestion Spec Schema (`connector_ingestion_spec.json`)

Tradebook ingests raw data from external REST webhooks, SQL replicas, and Parquet data lakes into PostgreSQL staging tables.

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ConnectorIngestionSpec",
  "type": "object",
  "required": ["connectorId", "name", "sourceType", "targetStagingTable", "watermarkColumn", "schemaMapping"],
  "properties": {
    "connectorId": { "type": "string", "format": "uuid" },
    "name": { "type": "string" },
    "sourceType": { "type": "string", "enum": ["REST_API", "WEBHOOK", "POSTGRES_REPLICA", "S3_PARQUET"] },
    "targetStagingTable": { "type": "string" },
    "watermarkColumn": { "type": "string" },
    "lookbackMinutes": { "type": "integer", "default": 60 },
    "authConfig": {
      "type": "object",
      "properties": {
        "authType": { "type": "string", "enum": ["BEARER_TOKEN", "API_KEY", "OAUTH2"] },
        "secretRef": { "type": "string" }
      }
    },
    "schemaMapping": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["sourceField", "targetColumn", "dataType"],
        "properties": {
          "sourceField": { "type": "string" },
          "targetColumn": { "type": "string" },
          "dataType": { "type": "string", "enum": ["VARCHAR", "NUMERIC", "TIMESTAMPTZ", "JSONB", "UUID"] },
          "transformExpression": { "type": "string" }
        }
      }
    }
  }
}
```

### 4.2 dbt Project & Models (`dbt_tradebook`)

#### `dbt_project.yml`
```yaml
name: 'dbt_tradebook'
version: '1.0.0'
config-version: 2

profile: 'tradebook_postgres'

model-paths: ["models"]
analysis-paths: ["analyses"]
test-paths: ["tests"]
seed-paths: ["seeds"]
macro-paths: ["macros"]
snapshot-paths: ["snapshots"]

clean-targets:
  - "target"
  - "dbt_packages"

models:
  dbt_tradebook:
    staging:
      +materialized: view
      +schema: staging
    marts:
      +materialized: table
      +schema: marts
```

#### Staging Model: `models/staging/stg_physical_deliveries.sql`
```sql
{{ config(materialized='view') }}

WITH raw_deliveries AS (
    SELECT
        id AS delivery_id,
        contract_id,
        contract_instance_id,
        book_type,
        supply_month,
        UPPER(status) AS status,
        volume_mwh::NUMERIC(28, 10) AS volume_mwh,
        revenue_eur::NUMERIC(28, 10) AS revenue_eur,
        tax_eur::NUMERIC(28, 10) AS tax_eur,
        vat_eur::NUMERIC(28, 10) AS vat_eur,
        invoice_amount_eur::NUMERIC(28, 10) AS invoice_amount_eur,
        report_id,
        created_at
    FROM {{ source('raw_ingestion', 'physical_deliveries') }}
    WHERE supply_month >= DATE_TRUNC('month', NOW()) - INTERVAL '24 months'
)
SELECT * FROM raw_deliveries
```

#### Mart Model: `models/marts/mart_delivery_pnl.sql`
```sql
{{ config(
    materialized='table',
    indexes=[
      {'columns': ['book_type', 'supply_month'], 'type': 'btree'}
    ]
) }}

WITH joined AS (
    SELECT
        d.delivery_id,
        d.book_type,
        d.supply_month,
        d.status,
        c.contract_name,
        c.product_type,
        cp.segment AS counterparty_segment,
        d.volume_mwh,
        d.revenue_eur,
        d.tax_eur,
        d.vat_eur,
        d.invoice_amount_eur
    FROM {{ ref('stg_physical_deliveries') }} d
    LEFT JOIN {{ source('raw_ingestion', 'contracts') }} c ON c.id = d.contract_id
    LEFT JOIN {{ source('raw_ingestion', 'counterparties') }} cp ON cp.id = c.counterparty_id
)
SELECT
    book_type,
    supply_month,
    contract_name,
    product_type,
    counterparty_segment,
    status,
    COUNT(delivery_id) AS delivery_count,
    SUM(volume_mwh) AS volume_mwh,
    SUM(revenue_eur) AS revenue_eur,
    SUM(tax_eur) AS tax_eur,
    SUM(vat_eur) AS vat_eur,
    SUM(invoice_amount_eur) AS invoice_amount_eur,
    SUM(revenue_eur) / NULLIF(SUM(volume_mwh), 0) AS avg_price_eur_mwh,
    SUM(vat_eur) / NULLIF(SUM(invoice_amount_eur), 0) AS vat_ratio
FROM joined
GROUP BY book_type, supply_month, contract_name, product_type, counterparty_segment, status
```

#### Schema Validation: `models/schema.yml`
```yaml
version: 2

sources:
  - name: raw_ingestion
    tables:
      - name: physical_deliveries
      - name: contracts
      - name: counterparties

models:
  - name: stg_physical_deliveries
    columns:
      - name: delivery_id
        tests:
          - unique
          - not_null
      - name: contract_instance_id
        tests:
          - not_null
      - name: volume_mwh
        tests:
          - not_null

  - name: mart_delivery_pnl
    columns:
      - name: delivery_count
        tests:
          - not_null
      - name: revenue_eur
        tests:
          - not_null
```

---

## 5. DuckDB WASM + Apache Arrow Edge Execution Architecture (<10ms Client Analytics)

### 5.1 Edge Execution System Architecture

```
+---------------------------------------------------------------------------------------------------+
client edge (<10ms queries)                  server query engine
+------------------------------------+       +------------------------------------------------------+
| React 19 Client UI                 |       | ASP.NET Core (.NET 9 API)                            |
|  - Dynamic Filter / Date Slider    |       |  - SemanticQueryCompiler.cs                          |
|  - Multi-Dimension Pivot Table     |       |  - Npgsql / Dapper SQL Query Execution               |
+------------------------------------+       |  - ApacheArrowStreamSerializer.cs                    |
                 |                           +------------------------------------------------------+
                 | (1. HTTP POST JsonQueryAst)                          |
                 v                                                      v
+---------------------------------------------------------------------------------------------------+
| 2. Server executes compiled SQL against Postgres/TimescaleDB                                      |
| 3. Serializes query result directly into Apache Arrow IPC Stream Buffer                           |
| 4. Returns binary `application/vnd.apache.arrow.stream` payload to browser client                 |
+---------------------------------------------------------------------------------------------------+
                 |
                 v
+---------------------------------------------------------------------------------------------------+
| 5. Frontend Web Worker (`DuckDBWorker.ts`) receives ArrayBuffer                                    |
| 6. Registers IPC buffer with DuckDB WASM (`db.registerArrowTableBuffer("analytics_cache")`)       |
| 7. User modifies date filters, pivots dimensions, or changes aggregations                         |
| 8. Client executes DuckDB WASM SQL against local Arrow table in memory                            |
| 9. Returns sub-10ms query result directly to React components (Zero Server Latency)              |
+---------------------------------------------------------------------------------------------------+
```

### 5.2 Server-Side Apache Arrow Stream Serializer (`ApacheArrowStreamSerializer.cs`)

```csharp
using Apache.Arrow;
using Apache.Arrow.Ipc;
using System.Data.Common;

namespace Tradebook.Analytics.Serializers;

public static class ApacheArrowStreamSerializer
{
    public static async Task SerializeDbDataReaderToArrowStreamAsync(
        DbDataReader reader, 
        Stream outputStream, 
        CancellationToken ct = default)
    {
        var schemaBuilder = new Schema.Builder();
        var fieldNames = new List<string>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var colName = reader.GetName(i);
            var colType = reader.GetFieldType(i);
            fieldNames.Add(colName);

            var arrowType = MapSystemTypeToArrowType(colType);
            schemaBuilder.Field(f => f.Name(colName).DataType(arrowType).Nullable(true));
        }

        var schema = schemaBuilder.Build();
        using var writer = new ArrowStreamWriter(outputStream, schema);

        const int batchSize = 10000;
        while (!reader.IsClosed && reader.HasRows)
        {
            var arrays = CreateArrowArraysFromReader(reader, fieldNames, batchSize, ct);
            if (arrays.Count == 0 || arrays[0].Length == 0) break;

            var recordBatch = new RecordBatch(schema, arrays, arrays[0].Length);
            await writer.WriteRecordBatchAsync(recordBatch, ct);
        }

        await writer.WriteEndAsync(ct);
    }

    private static IArrowType MapSystemTypeToArrowType(Type t)
    {
        if (t == typeof(int) || t == typeof(long)) return Int64Type.Default;
        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return DoubleType.Default;
        if (t == typeof(bool)) return BooleanType.Default;
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return TimestampType.Default;
        return StringType.Default;
    }

    private static List<IArrowArray> CreateArrowArraysFromReader(
        DbDataReader reader, 
        List<string> fields, 
        int batchSize, 
        CancellationToken ct)
    {
        // Build Apache Arrow Column Arrays
        var builders = fields.Select(_ => new StringArray.Builder()).ToList();
        int count = 0;

        while (count < batchSize && reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            for (int i = 0; i < fields.Count; i++)
            {
                if (reader.IsDBNull(i))
                {
                    builders[i].AppendNull();
                }
                else
                {
                    builders[i].Append(reader.GetValue(i).ToString()!);
                }
            }
            count++;
        }

        return builders.Select(b => (IArrowArray)b.Build()).ToList();
    }
}
```

### 5.3 Frontend DuckDB WASM Client Engine (`DuckDBClientEngine.ts`)

```typescript
import * as duckdb from '@duckdb/duckdb-wasm';
import duckdb_wasm from '@duckdb/duckdb-wasm/dist/duckdb-mvp.wasm?url';
import mvp_worker from '@duckdb/duckdb-wasm/dist/duckdb-node-mvp.worker.js?url';

export class DuckDBClientEngine {
  private db: duckdb.AsyncDuckDB | null = null;
  private conn: duckdb.AsyncDuckDBConnection | null = null;
  private isInitialized = false;

  public async initialize(): Promise<void> {
    if (this.isInitialized) return;

    const DUCKDB_BUNDLES: duckdb.DuckDBBundles = {
      mvp: {
        mainModule: duckdb_wasm,
        mainWorker: mvp_worker,
      },
    };

    const bundle = await duckdb.selectBundle(DUCKDB_BUNDLES);
    const worker = new Worker(bundle.mainWorker!);
    const logger = new duckdb.ConsoleLogger();

    this.db = new duckdb.AsyncDuckDB(logger, worker);
    await this.db.instantiate(bundle.mainModule, bundle.pthreadWorker);

    this.conn = await this.db.connect();
    this.isInitialized = true;
  }

  public async registerArrowBuffer(tableName: string, arrowIpcBuffer: Uint8Array): Promise<void> {
    if (!this.conn || !this.db) throw new Error("DuckDB WASM Engine is not initialized.");
    
    // Register zero-copy Arrow IPC Buffer as an in-memory DuckDB table
    await this.db.registerEmptyArrayBufferAsFile(tableName + '.arrow', arrowIpcBuffer);
    await this.conn.insertArrowFromIPCStream(arrowIpcBuffer, { name: tableName });
  }

  public async queryEdge<T = Record<string, unknown>>(sql: string): Promise<{ data: T[]; durationMs: number }> {
    if (!this.conn) throw new Error("DuckDB Connection unavailable.");

    const startTime = performance.now();
    const result = await this.conn.query(sql);
    const endTime = performance.now();

    const rows = result.toArray().map((row) => row.toJSON() as T);
    const durationMs = endTime - startTime;

    return { data: rows, durationMs };
  }

  public async close(): Promise<void> {
    if (this.conn) await this.conn.close();
    if (this.db) await this.db.terminate();
    this.isInitialized = false;
  }
}

export const duckDBEngine = new DuckDBClientEngine();
```

---

## 6. Subagent Step-by-Step Implementation Workflow

### Step 1: Semantic Model Specification Setup
1. Create `src/Backend/Tradebook.Analytics/SemanticModel/semantic_model.yaml` incorporating all dimensions, measures, derived metrics, and security rules.
2. Implement `SemanticModelDefinition.cs` to deserialize the YAML configuration using `YamlDotNet`.

### Step 2: C# Query Compiler Implementation
1. Implement `JsonQueryAst.cs` representing the query contract.
2. Implement `SemanticQueryCompiler.cs` to parse incoming JSON AST instances, generate dynamic SQL with parameter bindings, and build valid `GROUP BY` and `HAVING` clauses.
3. Write unit tests in `Tradebook.Analytics.Tests` validating compiler output for complex queries.

### Step 3: dbt Project & Connector Ingestion Setup
1. Create dbt project structure under `src/Analytics/dbt_tradebook`.
2. Write staging model `stg_physical_deliveries.sql`, mart model `mart_delivery_pnl.sql`, and `schema.yml`.
3. Create `connector_ingestion_spec.json` schema.

### Step 4: Server Arrow Serializer & API Endpoints
1. Add `Apache.Arrow` NuGet package to `Tradebook.Analytics.csproj`.
2. Implement `ApacheArrowStreamSerializer.cs` to convert DB query outputs into Arrow IPC stream buffers.
3. Implement FastEndpoints endpoint `QuerySemanticLayerEndpoint.cs` (`POST /api/v1/analytics/query`) that returns `application/vnd.apache.arrow.stream`.

### Step 5: Frontend DuckDB WASM Edge Integration
1. Install `@duckdb/duckdb-wasm` and `apache-arrow` npm dependencies in `src/Frontend`.
2. Create `DuckDBClientEngine.ts` and `DuckDBWorker.ts`.
3. Build React custom hook `useEdgeAnalytics.ts` that fetches Arrow IPC streams from backend API, registers buffers with DuckDB WASM, and exposes instant edge querying (`<10ms`).

---

## 7. Comprehensive Test Plan

### 7.1 Unit Tests (`Tradebook.Analytics.Tests`)
- **AST Compilation Test**: Validate that `SemanticQueryCompiler.Compile()` outputs valid ANSI SQL for single entity queries, multi-table join queries, and derived metric expressions.
- **SQL Injection Prevention Test**: Pass arbitrary malicious strings (e.g. `'; DROP TABLE physical_deliveries; --`) in filter values and confirm that all filter values are strictly bound to parameters (`@p0`, `@p1`).
- **Parameter Binding Test**: Verify that every compiled SQL query binds all filter values as `@p0`, `@p1` parameters with no string interpolation of user input.

### 7.2 dbt Model Tests
- Execute `dbt test` against a local PostgreSQL test database to verify unique key constraints, non-null columns, and transformation logic.

### 7.3 Integration & Edge Performance Benchmarks
- **Apache Arrow Serialization Benchmark**: Execute a query returning 50,000 rows; verify server serialization time is `<30ms`.
- **DuckDB WASM Edge Latency Test**: Register a 50,000-row Arrow IPC buffer in DuckDB WASM. Execute edge queries (e.g., `SELECT book_type, SUM(volume_mwh) FROM analytics_cache GROUP BY book_type`). Assert query duration is `<10ms`.

---

## 8. Agent Verification Steps

To independently verify Task 04 implementation, subagents and auditors must execute the following commands:

```bash
# 1. Verify C# Backend Analytics Engine & Compiler Unit Tests
dotnet build src/Backend/Tradebook.Analytics/Tradebook.Analytics.csproj
dotnet test tests/Tradebook.Analytics.Tests/Tradebook.Analytics.Tests.csproj

# 2. Verify dbt Transformation Models & Compile Verification
cd src/Analytics/dbt_tradebook
dbt compile --profiles-dir .
dbt test --profiles-dir .

# 3. Verify Frontend DuckDB WASM & Apache Arrow Integration
cd src/Frontend
npm run build
npm run test -- --filter=edgeAnalytics

# 4. End-to-End Arrow Stream & DuckDB WASM Execution Verification
dotnet run --project tests/performance/Tradebook.Benchmarks/EdgeAnalyticsBenchmark.csproj -c Release
```

---

## 9. Anti-Cheating & Integrity Guardrails

To preserve system integrity, the following implementation shortcuts are strictly prohibited:
1. **NO Hardcoded SQL Strings in API Endpoints**: All SQL generated for semantic queries MUST pass through `SemanticQueryCompiler.cs`. Hardcoding query strings directly in API handlers is an integrity violation.
2. **NO Hardcoded Mock AST Responses**: The query compiler MUST dynamically construct SQL based on input JSON AST payloads. Returning static pre-cooked SQL strings for test queries is prohibited.
3. **NO Dummy WASM Bypasses**: Client-side edge queries MUST genuinely execute inside DuckDB WASM using `insertArrowFromIPCStream()`. Bypassing DuckDB WASM by calculating array sums in plain JavaScript loop code is strictly forbidden.
4. **NO Security Filter Bypass**: The API layer MUST enforce role-claim authorization on every `/api/v1/analytics/query` call. Compiling a query without an authorized session, or removing the parameterized filter bindings, will cause an instant audit failure.
