using System.Data.Common;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tradebook.Core.Analytics;

public sealed class SemanticModelLoader
{
    private static readonly Regex Identifier = new("^[a-z_][a-z0-9_]*$", RegexOptions.Compiled);
    private static readonly HashSet<string> JoinTypes = ["inner", "left", "right", "full"];
    private static readonly HashSet<string> MeasureTypes = ["sum", "avg", "count", "count_distinct", "min", "max"];
    private readonly Dictionary<string, SemanticModelConfig> _models;
    private readonly Dictionary<(string Model, string Entity), IReadOnlyList<JoinChainStep>> _chains = [];

    public SemanticModelLoader(string? modelsDirectory = null)
    {
        var directory = modelsDirectory ?? Path.Combine(AppContext.BaseDirectory, "SemanticModels");
        if (!Directory.Exists(directory)) throw new InvalidOperationException($"Semantic model directory '{directory}' does not exist.");
        var deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
        _models = Directory.GetFiles(directory, "*.yaml").Select(path => deserializer.Deserialize<SemanticModelConfig>(File.ReadAllText(path)))
            .ToDictionary(model => model.SemanticModel.Name, StringComparer.Ordinal);
        if (_models.Count == 0) throw new InvalidOperationException("No semantic models were found.");
        foreach (var model in _models.Values) ValidateAndBuildChains(model);
    }

    public SemanticModelConfig GetModel(string name) => _models.TryGetValue(name, out var model) ? model : throw new SemanticValidationException($"Unknown semantic model '{name}'.");
    public IReadOnlyList<JoinChainStep> JoinChainFor(string modelName, string entityName) => _chains.TryGetValue((modelName, entityName), out var chain) ? chain : throw new SemanticValidationException($"Entity '{entityName}' is unreachable in semantic model '{modelName}'.");

    public async Task ValidateDatabaseSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        foreach (var model in _models.Values)
        foreach (var entity in model.SemanticModel.Entities)
        foreach (var column in entity.Columns)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table AND column_name = @column";
            AddParameter(command, "@table", entity.Table); AddParameter(command, "@column", column);
            if (await command.ExecuteScalarAsync(cancellationToken) is null) throw new InvalidOperationException($"Semantic model declares missing column '{entity.Table}.{column}'.");
        }
    }

    private void ValidateAndBuildChains(SemanticModelConfig config)
    {
        var model = config.SemanticModel ?? throw new InvalidOperationException("Semantic model is required.");
        var entities = model.Entities.ToDictionary(x => x.Name, StringComparer.Ordinal);
        ValidateIdentifier(model.Name, "model name");
        if (!entities.ContainsKey(model.TargetEntity)) throw new InvalidOperationException($"Target entity '{model.TargetEntity}' does not exist.");
        foreach (var entity in model.Entities) { ValidateIdentifier(entity.Name, "entity name"); ValidateIdentifier(entity.Table, "table"); ValidateIdentifier(entity.PrimaryKey, "primary key"); foreach (var column in entity.Columns) ValidateIdentifier(column, "column"); }
        foreach (var join in model.Joins)
        {
            ValidateIdentifier(join.Name, "join name");
            if (!JoinTypes.Contains(join.JoinType)) throw new InvalidOperationException($"Join '{join.Name}' has invalid type '{join.JoinType}'.");
            if (!entities.TryGetValue(join.LeftEntity, out var left) || !entities.TryGetValue(join.RightEntity, out var right) || !left.Columns.Contains(join.LeftColumn) || !right.Columns.Contains(join.RightColumn)) throw new InvalidOperationException($"Join '{join.Name}' references an unknown entity or column.");
        }
        foreach (var dimension in model.Dimensions) ValidateMember(dimension.Name, dimension.Entity, dimension.Sql, dimension.JsonbKey, entities, "dimension");
        foreach (var measure in model.Measures) { ValidateMember(measure.Name, measure.Entity, measure.Sql, null, entities, "measure"); if (!MeasureTypes.Contains(measure.Type)) throw new InvalidOperationException($"Measure '{measure.Name}' has unsupported type '{measure.Type}'."); }
        var measures = model.Measures.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var metric in model.Metrics) ValidateMetric(metric, measures);
        BuildChains(config.SemanticModel.Name, model);
    }

    private void BuildChains(string modelName, SemanticModelRoot model)
    {
        var visited = new Dictionary<string, List<JoinChainStep>>(StringComparer.Ordinal) { [model.TargetEntity] = [] };
        var queue = new Queue<string>(); queue.Enqueue(model.TargetEntity);
        while (queue.TryDequeue(out var current))
        foreach (var join in model.Joins)
        {
            var next = join.LeftEntity == current ? join.RightEntity : join.RightEntity == current ? join.LeftEntity : null;
            if (next is null || visited.ContainsKey(next)) continue;
            visited[next] = [.. visited[current], new JoinChainStep(join, next)]; queue.Enqueue(next);
        }
        foreach (var entity in model.Entities)
        {
            if (!visited.TryGetValue(entity.Name, out var chain)) throw new InvalidOperationException($"Entity '{entity.Name}' is not reachable from '{model.TargetEntity}'.");
            _chains[(modelName, entity.Name)] = chain;
        }
    }

    private static void ValidateMember(string name, string entity, string sql, string? jsonbKey, IReadOnlyDictionary<string, EntityDefinition> entities, string kind)
    { ValidateIdentifier(name, $"{kind} name"); ValidateIdentifier(entity, "entity"); ValidateIdentifier(sql, "sql"); if (!entities.TryGetValue(entity, out var definition) || !definition.Columns.Contains(sql)) throw new InvalidOperationException($"{kind} '{name}' references an unknown entity column."); if (jsonbKey is not null) ValidateIdentifier(jsonbKey, "jsonb key"); }
    private static void ValidateMetric(MetricDefinition metric, HashSet<string> measures)
    { ValidateIdentifier(metric.Name, "metric name"); if (!Regex.IsMatch(metric.Expression, "^[A-Za-z0-9_\\s().,+*/-]+$")) throw new InvalidOperationException($"Metric '{metric.Name}' contains forbidden characters."); foreach (Match token in Regex.Matches(metric.Expression, "[A-Za-z_][A-Za-z0-9_]*")) if (!token.Value.Equals("NULLIF", StringComparison.OrdinalIgnoreCase) && !measures.Contains(token.Value)) throw new InvalidOperationException($"Metric '{metric.Name}' references unknown measure '{token.Value}'."); }
    private static void ValidateIdentifier(string value, string label) { if (!Identifier.IsMatch(value)) throw new InvalidOperationException($"Invalid {label} '{value}'."); }
    private static void AddParameter(DbCommand command, string name, object value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
}
