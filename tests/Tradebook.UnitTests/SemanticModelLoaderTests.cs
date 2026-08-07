using Tradebook.Core.Analytics;

namespace Tradebook.UnitTests;

public sealed class SemanticModelLoaderTests
{
    private const string ValidModel = """
        version: "1.0"
        semantic_model:
          name: valid_model
          description: Test model.
          target_entity: root
          entities:
            - name: root
              table: root_table
              primary_key: id
              columns: [id, value, occurred_on, attributes]
              description: Root entity.
          joins: []
          dimensions:
            - { name: root_id, entity: root, type: string, sql: id }
            - { name: occurred_on, entity: root, type: date, sql: occurred_on, granularity: [day, month] }
            - { name: quality, entity: root, type: string, sql: attributes, jsonb_key: quality }
          measures:
            - { name: value_total, entity: root, type: sum, sql: value }
          metrics:
            - { name: value_ratio, description: Test metric., expression: "value_total / NULLIF(value_total, 0)" }
        """;

    [Fact]
    public void Valid_models_are_loaded_and_target_chain_is_empty()
    {
        using var files = ModelFiles.Create(ValidModel);

        var loader = new SemanticModelLoader(files.Directory);

        Assert.Equal("valid_model", loader.GetModel("valid_model").SemanticModel.Name);
        Assert.Empty(loader.JoinChainFor("valid_model", "root"));
    }

    [Theory]
    [InlineData("sql: value", "sql: value + 1")]
    [InlineData("jsonb_key: quality", "jsonb_key: quality->>'x'")]
    [InlineData("table: root_table", "table: root_table;drop")]
    public void Raw_sql_and_invalid_identifiers_fail_loading(string original, string replacement)
    {
        using var files = ModelFiles.Create(ValidModel.Replace(original, replacement, StringComparison.Ordinal));

        Assert.Throws<InvalidOperationException>(() => new SemanticModelLoader(files.Directory));
    }

    [Theory]
    [InlineData("value_total / NULLIF(value_total, 0)", "value_total); DROP TABLE root_table; --")]
    [InlineData("value_total / NULLIF(value_total, 0)", "unknown_measure / 2")]
    [InlineData("value_total / NULLIF(value_total, 0)", "ABS(value_total)")]
    public void Metric_expressions_accept_only_the_closed_grammar(
        string original,
        string replacement)
    {
        using var files = ModelFiles.Create(ValidModel.Replace(original, replacement, StringComparison.Ordinal));

        Assert.Throws<InvalidOperationException>(() => new SemanticModelLoader(files.Directory));
    }

    [Fact]
    public void Duplicate_member_names_fail_loading()
    {
        var duplicate = ValidModel.Replace(
            "- { name: occurred_on, entity: root, type: date, sql: occurred_on, granularity: [day, month] }",
            "- { name: occurred_on, entity: root, type: date, sql: occurred_on, granularity: [day, month] }\n" +
            "    - { name: root_id, entity: root, type: string, sql: id }",
            StringComparison.Ordinal);
        using var files = ModelFiles.Create(duplicate);

        Assert.Throws<InvalidOperationException>(() => new SemanticModelLoader(files.Directory));
    }

    [Fact]
    public void Non_date_dimensions_cannot_declare_granularities()
    {
        using var files = ModelFiles.Create(ValidModel.Replace(
            "{ name: root_id, entity: root, type: string, sql: id }",
            "{ name: root_id, entity: root, type: string, sql: id, granularity: [month] }",
            StringComparison.Ordinal));

        Assert.Throws<InvalidOperationException>(() => new SemanticModelLoader(files.Directory));
    }

    [Fact]
    public void Every_entity_must_be_reachable_from_the_target()
    {
        var orphan = ValidModel.Replace(
            "  joins: []",
            "    - name: orphan\n" +
            "      table: orphan_table\n" +
            "      primary_key: id\n" +
            "      columns: [id]\n" +
            "      description: Orphan entity.\n" +
            "  joins: []",
            StringComparison.Ordinal);
        using var files = ModelFiles.Create(orphan);

        Assert.Throws<InvalidOperationException>(() => new SemanticModelLoader(files.Directory));
    }

    [Fact]
    public void Duplicate_model_names_across_files_fail_loading()
    {
        using var files = ModelFiles.Create(ValidModel, ValidModel);

        Assert.Throws<InvalidOperationException>(() => new SemanticModelLoader(files.Directory));
    }

    [Fact]
    public void Member_names_cannot_be_ambiguous_across_semantic_kinds()
    {
        using var files = ModelFiles.Create(ValidModel.Replace(
            "name: value_ratio",
            "name: value_total",
            StringComparison.Ordinal));

        var exception = Assert.Throws<InvalidOperationException>(
            () => new SemanticModelLoader(files.Directory));
        Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reverse_traversal_inverts_directional_outer_joins()
    {
        const string reverseJoinModel = """
            version: "1.0"
            semantic_model:
              name: reverse_join_model
              description: Exercises reverse join traversal.
              target_entity: right_entity
              entities:
                - { name: left_entity, table: left_table, primary_key: id, columns: [id, name] }
                - { name: right_entity, table: right_table, primary_key: id, columns: [id, left_id] }
              joins:
                - { name: left_to_right, left_entity: left_entity, right_entity: right_entity, join_type: left, relationship: one_to_many, left_column: id, right_column: left_id }
              dimensions:
                - { name: left_name, entity: left_entity, type: string, sql: name }
              measures: []
              metrics: []
            """;
        using var files = ModelFiles.Create(reverseJoinModel);
        var loader = new SemanticModelLoader(files.Directory);
        var compiler = new SemanticQueryCompiler(loader);

        var query = compiler.Compile(new JsonQueryAst(
            "reverse_join_model", null, null, ["left_name"],
            null, null, null, null, null));

        Assert.Contains("FROM right_table", query.SqlText);
        Assert.Contains("RIGHT JOIN left_table", query.SqlText);
    }

    private sealed class ModelFiles : IDisposable
    {
        private ModelFiles(string directory) => Directory = directory;

        public string Directory { get; }

        public static ModelFiles Create(params string[] models)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"tradebook-semantic-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(directory);
            for (var index = 0; index < models.Length; index++)
            {
                File.WriteAllText(Path.Combine(directory, $"model-{index}.yaml"), models[index]);
            }

            return new ModelFiles(directory);
        }

        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }
}
