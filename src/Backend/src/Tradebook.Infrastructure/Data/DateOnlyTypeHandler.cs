using System.Globalization;
using Dapper;

namespace Tradebook.Infrastructure.Data;

internal sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) =>
        value switch
        {
            DateOnly date => date,
            DateTime timestamp => DateOnly.FromDateTime(timestamp),
            _ => DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture),
        };

    public override void SetValue(System.Data.IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = System.Data.DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }
}
