using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tradebook.Core.Analytics;

public sealed class SemanticModelLoader
{
    private static readonly Regex Identifier = new(
        "^[a-z_][a-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> JoinTypes =
        new(["inner", "left", "right", "full"], StringComparer.Ordinal);

    private static readonly HashSet<string> RelationshipTypes =
        new(["one_to_one", "one_to_many", "many_to_one", "many_to_many"], StringComparer.Ordinal);

    private static readonly HashSet<string> DimensionTypes =
        new(["string", "date", "number", "boolean"], StringComparer.Ordinal);

    private static readonly HashSet<string> MeasureTypes =
        new(["sum", "avg", "count", "count_distinct", "min", "max"], StringComparer.Ordinal);

    private static readonly HashSet<string> Granularities =
        new(["day", "week", "month", "quarter", "year"], StringComparer.Ordinal);

    private static readonly HashSet<string> NumericDatabaseTypes = new(
        [
            "smallint", "integer", "bigint", "decimal", "numeric", "real",
            "double precision", "smallserial", "serial", "bigserial", "money"
        ],
        StringComparer.Ordinal);

    private static readonly HashSet<string> DateDatabaseTypes = new(
        ["date", "timestamp without time zone", "timestamp with time zone"],
        StringComparer.Ordinal);

    private readonly Dictionary<string, SemanticModelConfig> _models = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Model, string Entity), IReadOnlyList<JoinChainStep>> _chains = [];

    public SemanticModelLoader(string? modelsDirectory = null)
    {
        var directory = modelsDirectory ?? Path.Combine(AppContext.BaseDirectory, "SemanticModels");
        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException($"Semantic model directory '{directory}' does not exist.");
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        foreach (var path in Directory.GetFiles(directory, "*.yaml").Order(StringComparer.Ordinal))
        {
            SemanticModelConfig config;
            try
            {
                config = deserializer.Deserialize<SemanticModelConfig>(File.ReadAllText(path))
                    ?? throw new InvalidOperationException("The YAML document is empty.");
            }
            catch (Exception exception) when (exception is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Semantic model file '{Path.GetFileName(path)}' is invalid.",
                    exception);
            }

            ValidateAndBuildChains(config);
            if (!_models.TryAdd(config.SemanticModel.Name, config))
            {
                throw new InvalidOperationException(
                    $"Duplicate semantic model name '{config.SemanticModel.Name}'.");
            }
        }

        if (_models.Count == 0)
        {
            throw new InvalidOperationException("No semantic models were found.");
        }
    }

    public SemanticModelConfig GetModel(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_models.TryGetValue(name, out var model))
        {
            throw new SemanticValidationException($"Unknown semantic model '{name}'.");
        }

        return model;
    }

    public IReadOnlyList<JoinChainStep> JoinChainFor(string modelName, string entityName) =>
        _chains.TryGetValue((modelName, entityName), out var chain)
            ? chain
            : throw new SemanticValidationException(
                $"Entity '{entityName}' is unreachable in semantic model '{modelName}'.");

    public async Task ValidateDatabaseSchemaAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        foreach (var model in _models.Values)
        {
            foreach (var entity in model.SemanticModel.Entities)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT column_name, data_type
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = @table
                    """;
                AddParameter(command, "@table", entity.Table);

                var columns = new Dictionary<string, string>(StringComparer.Ordinal);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    columns[reader.GetString(0)] = reader.GetString(1);
                }

                if (columns.Count == 0)
                {
                    throw new SemanticSchemaMismatchException(
                        $"Semantic model declares missing table 'public.{entity.Table}'.");
                }

                var missing = entity.Columns.Where(column => !columns.ContainsKey(column)).ToArray();
                if (missing.Length > 0)
                {
                    throw new SemanticSchemaMismatchException(
                        $"Semantic model declares missing columns on '{entity.Table}': {string.Join(", ", missing)}.");
                }

                foreach (var dimension in model.SemanticModel.Dimensions.Where(
                             dimension => dimension.Entity == entity.Name && dimension.JsonbKey is not null))
                {
                    if (!columns[dimension.Sql].Equals("jsonb", StringComparison.Ordinal))
                    {
                        throw new SemanticSchemaMismatchException(
                            $"Dimension '{dimension.Name}' declares jsonb_key for non-jsonb column " +
                            $"'{entity.Table}.{dimension.Sql}'.");
                    }
                }

                foreach (var dimension in model.SemanticModel.Dimensions.Where(
                             dimension => dimension.Entity == entity.Name && dimension.JsonbKey is null))
                {
                    var databaseType = columns[dimension.Sql];
                    if (dimension.Type == "date" && !DateDatabaseTypes.Contains(databaseType) ||
                        dimension.Type == "number" && !NumericDatabaseTypes.Contains(databaseType) ||
                        dimension.Type == "boolean" && databaseType != "boolean")
                    {
                        throw new SemanticSchemaMismatchException(
                            $"Dimension '{dimension.Name}' declares type '{dimension.Type}' but " +
                            $"'{entity.Table}.{dimension.Sql}' has database type '{databaseType}'.");
                    }
                }

                foreach (var measure in model.SemanticModel.Measures.Where(
                             measure => measure.Entity == entity.Name &&
                                        measure.Type is "sum" or "avg"))
                {
                    var databaseType = columns[measure.Sql];
                    if (!NumericDatabaseTypes.Contains(databaseType))
                    {
                        throw new SemanticSchemaMismatchException(
                            $"Measure '{measure.Name}' uses '{measure.Type}' but " +
                            $"'{entity.Table}.{measure.Sql}' has database type '{databaseType}'.");
                    }
                }
            }
        }
    }

    private void ValidateAndBuildChains(SemanticModelConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Version))
        {
            throw new InvalidOperationException("Semantic model version is required.");
        }

        var model = config.SemanticModel
            ?? throw new InvalidOperationException("Semantic model is required.");
        ValidateIdentifier(model.Name, "model name");
        ValidateIdentifier(model.TargetEntity, "target entity");

        EnsureUnique(model.Entities, entity => entity.Name, "entity");
        EnsureUnique(model.Entities, entity => entity.Table, "entity table");
        EnsureUnique(model.Joins, join => join.Name, "join");
        EnsureUnique(model.Dimensions, dimension => dimension.Name, "dimension");
        EnsureUnique(model.Measures, measure => measure.Name, "measure");
        EnsureUnique(model.Metrics, metric => metric.Name, "metric");
        EnsureMemberNamesAreUnambiguous(model);

        var entities = model.Entities.ToDictionary(entity => entity.Name, StringComparer.Ordinal);
        if (!entities.ContainsKey(model.TargetEntity))
        {
            throw new InvalidOperationException(
                $"Target entity '{model.TargetEntity}' does not exist.");
        }

        foreach (var entity in model.Entities)
        {
            ValidateIdentifier(entity.Name, "entity name");
            ValidateIdentifier(entity.Table, "table");
            ValidateIdentifier(entity.PrimaryKey, "primary key");
            EnsureUnique(entity.Columns, column => column, $"column on entity '{entity.Name}'");
            foreach (var column in entity.Columns)
            {
                ValidateIdentifier(column, "column");
            }

            if (!entity.Columns.Contains(entity.PrimaryKey, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Entity '{entity.Name}' primary key '{entity.PrimaryKey}' is not a declared column.");
            }
        }

        foreach (var join in model.Joins)
        {
            ValidateIdentifier(join.Name, "join name");
            ValidateIdentifier(join.LeftEntity, "left entity");
            ValidateIdentifier(join.RightEntity, "right entity");
            ValidateIdentifier(join.LeftColumn, "left join column");
            ValidateIdentifier(join.RightColumn, "right join column");

            if (!JoinTypes.Contains(join.JoinType))
            {
                throw new InvalidOperationException(
                    $"Join '{join.Name}' has invalid type '{join.JoinType}'.");
            }

            if (!RelationshipTypes.Contains(join.Relationship))
            {
                throw new InvalidOperationException(
                    $"Join '{join.Name}' has invalid relationship '{join.Relationship}'.");
            }

            if (!entities.TryGetValue(join.LeftEntity, out var left) ||
                !entities.TryGetValue(join.RightEntity, out var right) ||
                !left.Columns.Contains(join.LeftColumn, StringComparer.Ordinal) ||
                !right.Columns.Contains(join.RightColumn, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Join '{join.Name}' references an unknown entity or column.");
            }
        }

        foreach (var dimension in model.Dimensions)
        {
            ValidateMember(
                dimension.Name,
                dimension.Entity,
                dimension.Sql,
                dimension.JsonbKey,
                entities,
                "dimension");

            if (!DimensionTypes.Contains(dimension.Type))
            {
                throw new InvalidOperationException(
                    $"Dimension '{dimension.Name}' has unsupported type '{dimension.Type}'.");
            }

            if (dimension.Granularity is { Count: > 0 } granularities)
            {
                if (!dimension.Type.Equals("date", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Non-date dimension '{dimension.Name}' cannot declare granularities.");
                }

                EnsureUnique(granularities, granularity => granularity,
                    $"granularity on dimension '{dimension.Name}'");
                foreach (var granularity in granularities)
                {
                    if (!Granularities.Contains(granularity))
                    {
                        throw new InvalidOperationException(
                            $"Dimension '{dimension.Name}' has unsupported granularity '{granularity}'.");
                    }
                }
            }
        }

        foreach (var measure in model.Measures)
        {
            ValidateMember(
                measure.Name,
                measure.Entity,
                measure.Sql,
                null,
                entities,
                "measure");
            if (!MeasureTypes.Contains(measure.Type))
            {
                throw new InvalidOperationException(
                    $"Measure '{measure.Name}' has unsupported type '{measure.Type}'.");
            }
        }

        var measures = model.Measures
            .Select(measure => measure.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var metric in model.Metrics)
        {
            ValidateIdentifier(metric.Name, "metric name");
            try
            {
                MetricExpressionParser.Rewrite(metric.Expression, measure =>
                    measures.Contains(measure)
                        ? measure
                        : throw new InvalidOperationException(
                            $"Metric '{metric.Name}' references unknown measure '{measure}'."));
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    $"Metric '{metric.Name}' has an invalid expression: {exception.Message}",
                    exception);
            }
        }

        BuildChains(model.Name, model);
    }

    private void BuildChains(string modelName, SemanticModelRoot model)
    {
        var visited = new Dictionary<string, List<JoinChainStep>>(StringComparer.Ordinal)
        {
            [model.TargetEntity] = []
        };
        var queue = new Queue<string>();
        queue.Enqueue(model.TargetEntity);

        while (queue.TryDequeue(out var current))
        {
            foreach (var join in model.Joins)
            {
                var next = join.LeftEntity == current
                    ? join.RightEntity
                    : join.RightEntity == current
                        ? join.LeftEntity
                        : null;
                if (next is null || visited.ContainsKey(next))
                {
                    continue;
                }

                visited[next] = [.. visited[current], new JoinChainStep(join, next)];
                queue.Enqueue(next);
            }
        }

        foreach (var entity in model.Entities)
        {
            if (!visited.TryGetValue(entity.Name, out var chain))
            {
                throw new InvalidOperationException(
                    $"Entity '{entity.Name}' is not reachable from '{model.TargetEntity}'.");
            }

            _chains[(modelName, entity.Name)] = chain;
        }
    }

    private static void ValidateMember(
        string name,
        string entity,
        string sql,
        string? jsonbKey,
        IReadOnlyDictionary<string, EntityDefinition> entities,
        string kind)
    {
        ValidateIdentifier(name, $"{kind} name");
        ValidateIdentifier(entity, "entity");
        ValidateIdentifier(sql, "sql");
        if (!entities.TryGetValue(entity, out var definition) ||
            !definition.Columns.Contains(sql, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{kind} '{name}' references an unknown entity column.");
        }

        if (jsonbKey is not null)
        {
            ValidateIdentifier(jsonbKey, "jsonb key");
        }
    }

    private static void EnsureUnique<T>(
        IEnumerable<T> values,
        Func<T, string> keySelector,
        string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var key = keySelector(value);
            if (!seen.Add(key))
            {
                throw new InvalidOperationException($"Duplicate {label} name '{key}'.");
            }
        }
    }

    private static void EnsureMemberNamesAreUnambiguous(SemanticModelRoot model)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, kind) in model.Dimensions.Select(dimension => (dimension.Name, "dimension"))
                     .Concat(model.Measures.Select(measure => (measure.Name, "measure")))
                     .Concat(model.Metrics.Select(metric => (metric.Name, "metric"))))
        {
            if (!names.Add(name))
            {
                throw new InvalidOperationException(
                    $"Semantic member name '{name}' is ambiguous across dimensions, measures or metrics " +
                    $"(duplicate {kind}).");
            }
        }
    }

    private static void ValidateIdentifier(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || !Identifier.IsMatch(value))
        {
            throw new InvalidOperationException($"Invalid {label} '{value}'.");
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
