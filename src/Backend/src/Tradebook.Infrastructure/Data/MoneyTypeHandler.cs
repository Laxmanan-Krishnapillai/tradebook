using System.Data;
using System.Globalization;
using Dapper;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Infrastructure.Data;

/// <summary>
/// Hand-written Dapper handlers for the money value objects. The Vogen-generated
/// handlers parse strings with the current culture, which the banned-API gate
/// (RS0030) rejects; these use <see cref="CultureInfo.InvariantCulture"/>.
/// </summary>
internal abstract class MoneyTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    protected abstract T FromDecimal(decimal value);

    protected abstract decimal ToDecimal(T value);

    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.DbType = DbType.Decimal;
        parameter.Value = value is null ? DBNull.Value : ToDecimal(value);
    }

    public override T Parse(object value) =>
        value switch
        {
            decimal decimalValue => FromDecimal(decimalValue),
            int intValue => FromDecimal(intValue),
            long longValue => FromDecimal(longValue),
            double doubleValue => FromDecimal((decimal)doubleValue),
            string stringValue
                when decimal.TryParse(
                    stringValue,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var parsed
                ) => FromDecimal(parsed),
            _ => throw new InvalidCastException(
                $"Unable to cast object of type {value.GetType()} to {typeof(T).Name}"
            ),
        };
}
