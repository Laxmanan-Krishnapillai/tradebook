using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.MarketPrices;

public sealed class GetMarketPriceHistoryEndpoint(IMarketPriceRepository repository)
    : Endpoint<GetMarketPriceHistoryRequest, GetMarketPriceHistoryResponse>
{
    public override void Configure()
    {
        Get("/api/v1/market-prices");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(
        GetMarketPriceHistoryRequest request,
        CancellationToken ct
    ) =>
        await (
            Send.OkAsync(
                await (repository.GetHistoryAsync(request, ct)).ConfigureAwait(false),
                cancellation: ct
            )
        ).ConfigureAwait(false);
}
