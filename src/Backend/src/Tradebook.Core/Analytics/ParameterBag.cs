namespace Tradebook.Core.Analytics;

public sealed class ParameterBag
{
    private readonly Dictionary<string, object> _parameters = [];
    private int _counter;

    public IReadOnlyDictionary<string, object> Parameters => _parameters;

    public string Bind(object value)
    {
        var name = $"@p{_counter++}";
        _parameters[name] = value;
        return name;
    }
}
