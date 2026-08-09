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
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1)
    );

    private static readonly HashSet<string> JoinTypes = new(
        ["inner", "left", "right", "full"],
        StringComparer.Ordinal
    );

    private static readonly HashSet<string> RelationshipTypes = new(
        ["one_to_one", "one_to_many", "many_to_one", "many_to_many"],
        StringComparer.Ordinal
    );

    private static readonly HashSet<string> DimensionTypes = new(
        ["string", "date", "number", "boolean"],
        StringComparer.Ordinal
    );

    private static readonly HashSet<string> MeasureTypes = new(
        ["sum", "avg", "count", "count_distinct", "min", "max"],
        StringComparer.Ordinal
    );

    private static readonly HashSet<string> Granularities = new(
        ["day", "week", "month", "quarter", "year"],
        StringComparer.Ordinal
    );

    private static readonly HashSet<string> NumericDatabaseTypes = new(
        [
            "smallint",
            "integer",
            "bigint",
            "decimal",
            "numeric",
            "real",
            "double precision",
            "smallserial",
            "serial",
            "bigserial",
            "money",
        ],
        StringComparer.Ordinal
    );

    private static readonly HashSet<string> DateDatabaseTypes = new(
        ["date", "timestamp without time zone", "timestamp with time zone"],
        StringComparer.Ordinal
    );

    private readonly Dictionary<string, SemanticModelConfig> _models = new(StringComparer.Ordinal);
    private readonly Dictionary<
        (string Model, string Entity),
        IReadOnlyList<JoinChainStep>
    > _chains = [];

    public SemanticModelLoader(string? modelsDirectory = null)
    {
        var directory = modelsDirectory ?? Path.Combine(AppContext.BaseDirectory, "SemanticModels");
        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException(
                $"Semantic model directory '{directory}' does not exist."
            );
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        foreach (var path in Directory.GetFiles(directory, "*.yaml").Order(StringComparer.Ordinal))
        {
            SemanticModelConfig config;
            try
            {
                config =
                    deserializer.Deserialize<SemanticModelConfig>(File.ReadAllText(path))
                    ?? throw new InvalidOperationException("The YAML document is empty.");
            }
            catch (Exception exception) when (exception is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Semantic model file '{Path.GetFileName(path)}' is invalid.",
                    exception
                );
            }

            ValidateAndBuildChains(config);
            if (!_models.TryAdd(config.SemanticModel.Name, config))
            {
                throw new InvalidOperationException(
                    $"Duplicate semantic model name '{config.SemanticModel.Name}'."
                );
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
                $"Entity '{entityName}' is unreachable in semantic model '{modelName}'."
            );

    public async Task ValidateDatabaseSchemaAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default
    )
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var model in _models.Values.Select(config => config.SemanticModel))
        {
            foreach (var entity in model.Entities)
            {
                var columns = await ReadColumnsAsync(connection, entity.Table, cancellationToken)
                    .ConfigureAwait(false);
                ValidateDeclaredColumns(entity, columns);
                ValidateJsonbDimensions(model, entity, columns);
                ValidateDimensionTypes(model, entity, columns);
                ValidateMeasureTypes(model, entity, columns);
            }
        }
    }

    private static async Task<Dictionary<string, string>> ReadColumnsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken
    )
    {
        var command = connection.CreateCommand();
        await using var configuredCommand = command.ConfigureAwait(false);
        command.CommandText = """
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table
            """;
        AddParameter(command, "@table", table);

        var columns = new Dictionary<string, string>(StringComparer.Ordinal);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var configuredReader = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }

        if (columns.Count == 0)
        {
            throw new SemanticSchemaMismatchException(
                $"Semantic model declares missing table 'public.{table}'."
            );
        }

        return columns;
    }

    private static void ValidateDeclaredColumns(
        EntityDefinition entity,
        Dictionary<string, string> columns
    )
    {
        var missing = entity.Columns.Where(column => !columns.ContainsKey(column)).ToArray();
        if (missing.Length > 0)
        {
            throw new SemanticSchemaMismatchException(
                $"Semantic model declares missing columns on '{entity.Table}': {string.Join(", ", missing)}."
            );
        }
    }

    private static void ValidateJsonbDimensions(
        SemanticModelRoot model,
        EntityDefinition entity,
        Dictionary<string, string> columns
    )
    {
        var mismatch = model.Dimensions.FirstOrDefault(dimension =>
            string.Equals(dimension.Entity, entity.Name, StringComparison.Ordinal)
            && dimension.JsonbKey is not null
            && !string.Equals(columns[dimension.Sql], "jsonb", StringComparison.Ordinal)
        );
        if (mismatch is not null)
        {
            throw new SemanticSchemaMismatchException(
                $"Dimension '{mismatch.Name}' declares jsonb_key for non-jsonb column "
                    + $"'{entity.Table}.{mismatch.Sql}'."
            );
        }
    }

    private static void ValidateDimensionTypes(
        SemanticModelRoot model,
        EntityDefinition entity,
        Dictionary<string, string> columns
    )
    {
        var mismatch = model.Dimensions.FirstOrDefault(dimension =>
            string.Equals(dimension.Entity, entity.Name, StringComparison.Ordinal)
            && dimension.JsonbKey is null
            && !DatabaseTypeMatches(dimension.Type, columns[dimension.Sql])
        );
        if (mismatch is not null)
        {
            var databaseType = columns[mismatch.Sql];
            throw new SemanticSchemaMismatchException(
                $"Dimension '{mismatch.Name}' declares type '{mismatch.Type}' but "
                    + $"'{entity.Table}.{mismatch.Sql}' has database type '{databaseType}'."
            );
        }
    }

    private static bool DatabaseTypeMatches(string dimensionType, string databaseType) =>
        dimensionType switch
        {
            "date" => DateDatabaseTypes.Contains(databaseType),
            "number" => NumericDatabaseTypes.Contains(databaseType),
            "boolean" => string.Equals(databaseType, "boolean", StringComparison.Ordinal),
            _ => true,
        };

    private static void ValidateMeasureTypes(
        SemanticModelRoot model,
        EntityDefinition entity,
        Dictionary<string, string> columns
    )
    {
        var mismatch = model.Measures.FirstOrDefault(measure =>
            string.Equals(measure.Entity, entity.Name, StringComparison.Ordinal)
            && measure.Type is "sum" or "avg"
            && !NumericDatabaseTypes.Contains(columns[measure.Sql])
        );
        if (mismatch is not null)
        {
            var databaseType = columns[mismatch.Sql];
            throw new SemanticSchemaMismatchException(
                $"Measure '{mismatch.Name}' uses '{mismatch.Type}' but "
                    + $"'{entity.Table}.{mismatch.Sql}' has database type '{databaseType}'."
            );
        }
    }

    private void ValidateAndBuildChains(SemanticModelConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Version))
        {
            throw new InvalidOperationException("Semantic model version is required.");
        }

        var model =
            config.SemanticModel
            ?? throw new InvalidOperationException("Semantic model is required.");
        ValidateModelHeader(model);
        var entities = CreateEntityIndex(model);
        ValidateEntities(model.Entities);
        ValidateJoins(model.Joins, entities);
        ValidateDimensions(model.Dimensions, entities);
        ValidateMeasures(model.Measures, entities);
        ValidateMetrics(model);
        BuildChains(model.Name, model);
    }

    private static void ValidateModelHeader(SemanticModelRoot model)
    {
        ValidateIdentifier(model.Name, "model name");
        ValidateIdentifier(model.TargetEntity, "target entity");
        EnsureUnique(model.Entities, entity => entity.Name, "entity");
        EnsureUnique(model.Entities, entity => entity.Table, "entity table");
        EnsureUnique(model.Joins, join => join.Name, "join");
        EnsureUnique(model.Dimensions, dimension => dimension.Name, "dimension");
        EnsureUnique(model.Measures, measure => measure.Name, "measure");
        EnsureUnique(model.Metrics, metric => metric.Name, "metric");
        EnsureMemberNamesAreUnambiguous(model);
    }

    private static Dictionary<string, EntityDefinition> CreateEntityIndex(SemanticModelRoot model)
    {
        var entities = model.Entities.ToDictionary(entity => entity.Name, StringComparer.Ordinal);
        if (!entities.ContainsKey(model.TargetEntity))
        {
            throw new InvalidOperationException(
                $"Target entity '{model.TargetEntity}' does not exist."
            );
        }

        return entities;
    }

    private static void ValidateEntities(IList<EntityDefinition> entities)
    {
        foreach (var entity in entities)
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
                    $"Entity '{entity.Name}' primary key '{entity.PrimaryKey}' is not a declared column."
                );
            }
        }
    }

    private static void ValidateJoins(
        IList<JoinDefinition> joins,
        Dictionary<string, EntityDefinition> entities
    )
    {
        foreach (var join in joins)
        {
            ValidateIdentifier(join.Name, "join name");
            ValidateIdentifier(join.LeftEntity, "left entity");
            ValidateIdentifier(join.RightEntity, "right entity");
            ValidateIdentifier(join.LeftColumn, "left join column");
            ValidateIdentifier(join.RightColumn, "right join column");

            if (!JoinTypes.Contains(join.JoinType))
            {
                throw new InvalidOperationException(
                    $"Join '{join.Name}' has invalid type '{join.JoinType}'."
                );
            }

            if (!RelationshipTypes.Contains(join.Relationship))
            {
                throw new InvalidOperationException(
                    $"Join '{join.Name}' has invalid relationship '{join.Relationship}'."
                );
            }

            if (
                !entities.TryGetValue(join.LeftEntity, out var left)
                || !entities.TryGetValue(join.RightEntity, out var right)
                || !left.Columns.Contains(join.LeftColumn, StringComparer.Ordinal)
                || !right.Columns.Contains(join.RightColumn, StringComparer.Ordinal)
            )
            {
                throw new InvalidOperationException(
                    $"Join '{join.Name}' references an unknown entity or column."
                );
            }
        }
    }

    private static void ValidateDimensions(
        IList<DimensionDefinition> dimensions,
        Dictionary<string, EntityDefinition> entities
    )
    {
        foreach (var dimension in dimensions)
        {
            ValidateMember(
                dimension.Name,
                dimension.Entity,
                dimension.Sql,
                dimension.JsonbKey,
                entities,
                "dimension"
            );

            if (!DimensionTypes.Contains(dimension.Type))
            {
                throw new InvalidOperationException(
                    $"Dimension '{dimension.Name}' has unsupported type '{dimension.Type}'."
                );
            }

            if (dimension.Granularity is not { Count: > 0 } granularities)
            {
                continue;
            }

            if (!string.Equals(dimension.Type, "date", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Non-date dimension '{dimension.Name}' cannot declare granularities."
                );
            }

            EnsureUnique(
                granularities,
                granularity => granularity,
                $"granularity on dimension '{dimension.Name}'"
            );
            var unsupported = granularities
                .Where(granularity => !Granularities.Contains(granularity))
                .Take(1)
                .ToArray();
            if (unsupported.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Dimension '{dimension.Name}' has unsupported granularity '{unsupported[0]}'."
                );
            }
        }
    }

    private static void ValidateMeasures(
        IList<MeasureDefinition> measures,
        Dictionary<string, EntityDefinition> entities
    )
    {
        foreach (var measure in measures)
        {
            ValidateMember(measure.Name, measure.Entity, measure.Sql, null, entities, "measure");
            if (!MeasureTypes.Contains(measure.Type))
            {
                throw new InvalidOperationException(
                    $"Measure '{measure.Name}' has unsupported type '{measure.Type}'."
                );
            }
        }
    }

    private static void ValidateMetrics(SemanticModelRoot model)
    {
        var measures = model
            .Measures.Select(measure => measure.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var metric in model.Metrics)
        {
            ValidateIdentifier(metric.Name, "metric name");
            try
            {
                MetricExpressionParser.Rewrite(
                    metric.Expression,
                    measure =>
                        measures.Contains(measure)
                            ? measure
                            : throw new InvalidOperationException(
                                $"Metric '{metric.Name}' references unknown measure '{measure}'."
                            )
                );
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    $"Metric '{metric.Name}' has an invalid expression: {exception.Message}",
                    exception
                );
            }
        }
    }

    private void BuildChains(string modelName, SemanticModelRoot model)
    {
        var visited = new Dictionary<string, List<JoinChainStep>>(StringComparer.Ordinal)
        {
            [model.TargetEntity] = [],
        };
        var queue = new Queue<string>();
        queue.Enqueue(model.TargetEntity);

        while (queue.TryDequeue(out var current))
        {
            foreach (var join in model.Joins)
            {
                var next = NextEntity(join, current);
                if (next is null || visited.ContainsKey(next))
                {
                    continue;
                }

                visited[next] = [.. visited[current], new JoinChainStep(join, next)];
                queue.Enqueue(next);
            }
        }

        foreach (var entityName in model.Entities.Select(entity => entity.Name))
        {
            if (!visited.TryGetValue(entityName, out var chain))
            {
                throw new InvalidOperationException(
                    $"Entity '{entityName}' is not reachable from '{model.TargetEntity}'."
                );
            }

            _chains[(modelName, entityName)] = chain;
        }
    }

    private static string? NextEntity(JoinDefinition join, string current)
    {
        if (string.Equals(join.LeftEntity, current, StringComparison.Ordinal))
        {
            return join.RightEntity;
        }

        return string.Equals(join.RightEntity, current, StringComparison.Ordinal)
            ? join.LeftEntity
            : null;
    }

    private static void ValidateMember(
        string name,
        string entity,
        string sql,
        string? jsonbKey,
        Dictionary<string, EntityDefinition> entities,
        string kind
    )
    {
        ValidateIdentifier(name, $"{kind} name");
        ValidateIdentifier(entity, "entity");
        ValidateIdentifier(sql, "sql");
        if (
            !entities.TryGetValue(entity, out var definition)
            || !definition.Columns.Contains(sql, StringComparer.Ordinal)
        )
        {
            throw new InvalidOperationException(
                $"{kind} '{name}' references an unknown entity column."
            );
        }

        if (jsonbKey is not null)
        {
            ValidateIdentifier(jsonbKey, "jsonb key");
        }
    }

    private static void EnsureUnique<T>(
        IEnumerable<T> values,
        Func<T, string> keySelector,
        string label
    )
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
        foreach (
            var (name, kind) in model
                .Dimensions.Select(dimension => (dimension.Name, "dimension"))
                .Concat(model.Measures.Select(measure => (measure.Name, "measure")))
                .Concat(model.Metrics.Select(metric => (metric.Name, "metric")))
        )
        {
            if (!names.Add(name))
            {
                throw new InvalidOperationException(
                    $"Semantic member name '{name}' is ambiguous across dimensions, measures or metrics "
                        + $"(duplicate {kind})."
                );
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
