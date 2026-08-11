namespace Tradebook.Core.Analytics;

public sealed class SemanticQueryCompiler(SemanticModelLoader loader)
{
    public CompiledSqlQuery Compile(JsonQueryAst ast)
    {
        ArgumentNullException.ThrowIfNull(ast);
        SemanticQueryShapeValidator.Validate(ast);

        var context = new SemanticCompilationContext(loader.GetModel(ast.ModelName).SemanticModel);
        SemanticProjectionBuilder.Add(ast, context);

        if (!context.HasProjections)
        {
            throw new SemanticValidationException(
                "Query selects no dimensions, measures or metrics."
            );
        }

        SemanticFilterBuilder.Add(ast.Filters, context);
        return new CompiledSqlQuery
        {
            SqlText = SemanticSqlBuilder.Build(ast, context, loader),
            Parameters = context.Parameters.Parameters,
            ResultColumnNames = context.ResultColumnNames,
        };
    }
}
