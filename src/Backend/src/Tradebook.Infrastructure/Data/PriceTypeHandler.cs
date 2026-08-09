using System.Data;
using System.Globalization;
using Dapper;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Infrastructure.Data;

internal sealed class PriceTypeHandler : MoneyTypeHandler<Price>
{
    protected override Price FromDecimal(decimal value) => Price.From(value);

    protected override decimal ToDecimal(Price value) => value.Value;
}
