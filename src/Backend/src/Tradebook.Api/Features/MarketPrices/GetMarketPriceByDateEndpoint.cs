using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.MarketPrices;

public sealed class GetMarketPriceByDateEndpoint(IMarketPriceRepository repository)
    : Endpoint<GetMarketPriceByDateRequest, MarketPriceDetailsDto>
{
    public override void Configure()
    {
        Get("/api/v1/market-prices/{priceDate}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(
        GetMarketPriceByDateRequest request,
        CancellationToken ct
    )
    {
        var result = await (repository.GetByDateAsync(request.PriceDate, ct)).ConfigureAwait(false);
        if (result is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (Send.OkAsync(result, cancellation: ct)).ConfigureAwait(false);
    }
}
