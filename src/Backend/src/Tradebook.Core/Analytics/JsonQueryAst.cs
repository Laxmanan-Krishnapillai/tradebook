namespace Tradebook.Core.Analytics;

public sealed record JsonQueryAst(string ModelName, List<string>? Measures, List<string>? Metrics, List<string>? Dimensions, List<TimeDimensionQuery>? TimeDimensions, List<FilterQuery>? Filters, List<SortQuery>? Sorts, int? Limit, int? Offset);
public sealed record TimeDimensionQuery(string Dimension, string Granularity, string[]? DateRange);
public sealed record FilterQuery(string Member, FilterOperator Operator, List<object> Values);
public sealed record SortQuery(string Member, string Direction);
public enum FilterOperator { Equals, NotEquals, Contains, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, In, NotIn }
