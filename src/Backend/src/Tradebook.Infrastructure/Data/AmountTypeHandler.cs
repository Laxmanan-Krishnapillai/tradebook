using System.Data;
using System.Globalization;
using Dapper;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Infrastructure.Data;

internal sealed class AmountTypeHandler : MoneyTypeHandler<Amount>
{
    protected override Amount FromDecimal(decimal value) => Amount.From(value);

    protected override decimal ToDecimal(Amount value) => value.Value;
}
