namespace Tradebook.Core.Analytics;

internal sealed class SemanticCompilationContext(SemanticModelRoot model)
{
    private readonly HashSet<string> _selectedColumns = new(StringComparer.Ordinal);

    public SemanticModelRoot Model { get; } = model;

    public ParameterBag Parameters { get; } = new();

    public List<string> SelectClauses { get; } = [];

    public List<string> GroupByClauses { get; } = [];

    public List<string> WhereClauses { get; } = [];

    public List<string> HavingClauses { get; } = [];

    public List<string> ResultColumnNames { get; } = [];

    public HashSet<string> RequiredEntities { get; } = new(StringComparer.Ordinal);

    public bool HasProjections => SelectClauses.Count > 0;

    public void AddProjection(string alias, string sql, string? groupBySql = null)
    {
        if (!_selectedColumns.Add(alias))
        {
            throw new SemanticValidationException(
                $"Result column '{alias}' is selected more than once."
            );
        }

        SelectClauses.Add(sql);
        ResultColumnNames.Add(alias);
        if (groupBySql is not null)
        {
            GroupByClauses.Add(groupBySql);
        }
    }

    public bool IsSelectedColumn(string name) => _selectedColumns.Contains(name);
}
