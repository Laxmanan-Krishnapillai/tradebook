using System.Text;
using System.Text.RegularExpressions;

namespace Tradebook.Core.Analytics;

public sealed class SemanticValidationException(string message) : Exception(message);
public sealed class ParameterBag
{
    private int _counter;
    public Dictionary<string, object> Parameters { get; } = [];
    public string Bind(object value) { var name = $"@p{_counter++}"; Parameters[name] = value; return name; }
}
public sealed class CompiledSqlQuery { public required string SqlText { get; init; } public required Dictionary<string, object> Parameters { get; init; } public required List<string> ResultColumnNames { get; init; } }

public sealed class SemanticQueryCompiler
{
    private static readonly Dictionary<string, string> Granularities = new(StringComparer.OrdinalIgnoreCase) { ["day"] = "day", ["week"] = "week", ["month"] = "month", ["quarter"] = "quarter", ["year"] = "year" };
    private static readonly Regex IdentifierToken = new("[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);
    private readonly SemanticModelLoader _loader;
    public SemanticQueryCompiler(SemanticModelLoader loader) => _loader = loader;

    public CompiledSqlQuery Compile(JsonQueryAst ast)
    {
        ArgumentNullException.ThrowIfNull(ast);
        var model = _loader.GetModel(ast.ModelName).SemanticModel;
        var bag = new ParameterBag(); var select = new List<string>(); var groupBy = new List<string>(); var where = new List<string>(); var having = new List<string>(); var columns = new List<string>(); var required = new HashSet<string>();
        foreach (var name in ast.Dimensions ?? []) { var d = Dimension(model, name); required.Add(d.Entity); var sql = DimensionSql(model, d); select.Add($"{sql} AS {d.Name}"); groupBy.Add(sql); columns.Add(d.Name); }
        foreach (var time in ast.TimeDimensions ?? [])
        {
            var d = Dimension(model, time.Dimension); required.Add(d.Entity); if (!Granularities.TryGetValue(time.Granularity, out var granularity)) throw new SemanticValidationException($"Unknown granularity '{time.Granularity}'.");
            var sql = DimensionSql(model, d); var bucket = $"date_trunc('{granularity}', {sql})"; var alias = $"{d.Name}_{granularity}"; select.Add($"{bucket} AS {alias}"); groupBy.Add(bucket); columns.Add(alias);
            if (time.DateRange is not null) { if (time.DateRange.Length != 2 || !DateTimeOffset.TryParse(time.DateRange[0], out var start) || !DateTimeOffset.TryParse(time.DateRange[1], out var end)) throw new SemanticValidationException($"Time dimension '{d.Name}' has an invalid date range."); where.Add($"{sql} >= {bag.Bind(start)} AND {sql} <= {bag.Bind(end)}"); }
        }
        foreach (var name in ast.Measures ?? []) { var m = Measure(model, name); required.Add(m.Entity); select.Add($"{Aggregate(model, m)} AS {m.Name}"); columns.Add(m.Name); }
        foreach (var name in ast.Metrics ?? []) { var metric = model.Metrics.FirstOrDefault(x => x.Name == name) ?? throw new SemanticValidationException($"Metric '{name}' not found."); select.Add($"{ExpandMetric(model, metric.Expression, required)} AS {metric.Name}"); columns.Add(metric.Name); }
        if (select.Count == 0) throw new SemanticValidationException("Query selects no dimensions, measures or metrics.");
        foreach (var filter in ast.Filters ?? [])
        {
            var d = model.Dimensions.FirstOrDefault(x => x.Name == filter.Member);
            if (d is not null) { required.Add(d.Entity); where.Add(Filter(DimensionSql(model, d), filter, bag)); continue; }
            var m = model.Measures.FirstOrDefault(x => x.Name == filter.Member);
            if (m is not null) { required.Add(m.Entity); having.Add(Filter(Aggregate(model, m), filter, bag)); continue; }
            throw new SemanticValidationException($"Unknown filter member '{filter.Member}'.");
        }
        var target = model.Entities.First(x => x.Name == model.TargetEntity); var builder = new StringBuilder("SELECT\n  "); builder.Append(string.Join(",\n  ", select)).AppendLine().AppendLine($"FROM {target.Table}");
        var joins = new HashSet<string>();
        foreach (var entity in model.Entities.Where(x => required.Contains(x.Name) && x.Name != target.Name)) foreach (var step in _loader.JoinChainFor(model.Name, entity.Name)) if (joins.Add(step.Join.Name))
        { var left = model.Entities.First(x => x.Name == step.Join.LeftEntity); var right = model.Entities.First(x => x.Name == step.Join.RightEntity); var newTable = model.Entities.First(x => x.Name == step.NewEntity).Table; builder.AppendLine($"{step.Join.JoinType.ToUpperInvariant()} JOIN {newTable} ON {left.Table}.{step.Join.LeftColumn} = {right.Table}.{step.Join.RightColumn}"); }
        if (where.Count > 0) builder.AppendLine("WHERE " + string.Join(" AND ", where)); if (groupBy.Count > 0) builder.AppendLine("GROUP BY " + string.Join(", ", groupBy)); if (having.Count > 0) builder.AppendLine("HAVING " + string.Join(" AND ", having));
        if (ast.Sorts is { Count: > 0 }) { var sorts = ast.Sorts.Select(s => { if (!columns.Contains(s.Member)) throw new SemanticValidationException($"Sort member '{s.Member}' is not a selected column of this query."); return s.Direction.ToLowerInvariant() switch { "asc" => $"{s.Member} ASC", "desc" => $"{s.Member} DESC", _ => throw new SemanticValidationException($"Invalid sort direction '{s.Direction}'.") }; }); builder.AppendLine("ORDER BY " + string.Join(", ", sorts)); }
        builder.AppendLine($"LIMIT {Math.Clamp(ast.Limit ?? 500, 1, 10000)} OFFSET {Math.Max(ast.Offset ?? 0, 0)}");
        return new CompiledSqlQuery { SqlText = builder.ToString(), Parameters = bag.Parameters, ResultColumnNames = columns };
    }

    private static DimensionDefinition Dimension(SemanticModelRoot model, string name) => model.Dimensions.FirstOrDefault(x => x.Name == name) ?? throw new SemanticValidationException($"Dimension '{name}' not found in semantic model.");
    private static MeasureDefinition Measure(SemanticModelRoot model, string name) => model.Measures.FirstOrDefault(x => x.Name == name) ?? throw new SemanticValidationException($"Measure '{name}' not found in semantic model.");
    private static string DimensionSql(SemanticModelRoot model, DimensionDefinition d) { var table = model.Entities.First(x => x.Name == d.Entity).Table; var col = $"{table}.{d.Sql}"; return d.JsonbKey is null ? col : $"{col} ->> '{d.JsonbKey}'"; }
    private static string Aggregate(SemanticModelRoot model, MeasureDefinition m) { var col = $"{model.Entities.First(x => x.Name == m.Entity).Table}.{m.Sql}"; return m.Type.ToLowerInvariant() switch { "sum" => $"SUM({col})", "avg" => $"AVG({col})", "count" => $"COUNT({col})", "count_distinct" => $"COUNT(DISTINCT {col})", "min" => $"MIN({col})", "max" => $"MAX({col})", _ => throw new SemanticValidationException($"Unsupported aggregation '{m.Type}'.") }; }
    private static string Filter(string target, FilterQuery f, ParameterBag bag) { if (f.Values is not { Count: > 0 }) throw new SemanticValidationException($"Filter on '{f.Member}' has no values."); return f.Operator switch { FilterOperator.Equals => $"{target} = {bag.Bind(f.Values[0])}", FilterOperator.NotEquals => $"{target} <> {bag.Bind(f.Values[0])}", FilterOperator.GreaterThan => $"{target} > {bag.Bind(f.Values[0])}", FilterOperator.GreaterThanOrEqual => $"{target} >= {bag.Bind(f.Values[0])}", FilterOperator.LessThan => $"{target} < {bag.Bind(f.Values[0])}", FilterOperator.LessThanOrEqual => $"{target} <= {bag.Bind(f.Values[0])}", FilterOperator.Contains => $"{target} ILIKE {bag.Bind($"%{f.Values[0]}%")}", FilterOperator.In => $"{target} IN ({string.Join(", ", f.Values.Select(bag.Bind))})", FilterOperator.NotIn => $"{target} NOT IN ({string.Join(", ", f.Values.Select(bag.Bind))})", _ => throw new SemanticValidationException($"Unsupported filter operator '{f.Operator}'.") }; }
    private static string ExpandMetric(SemanticModelRoot model, string expression, HashSet<string> required) => IdentifierToken.Replace(expression, m => { if (m.Value.Equals("NULLIF", StringComparison.OrdinalIgnoreCase)) return m.Value; var measure = Measure(model, m.Value); required.Add(measure.Entity); return Aggregate(model, measure); });
}
