using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.MarketPrices;

public sealed record GetMarketPriceByDateRequest
{
    public GetMarketPriceByDateRequest() { }

    [SetsRequiredMembers]
    public GetMarketPriceByDateRequest(DateOnly PriceDate) => this.PriceDate = PriceDate;

    public required DateOnly PriceDate { get; init; }
}
