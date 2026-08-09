using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.MarketPrices;

public sealed class DeleteMarketPriceValidator : Validator<DeleteMarketPriceRequest>
{
    public DeleteMarketPriceValidator()
    {
        RuleFor(x => x.PriceDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
