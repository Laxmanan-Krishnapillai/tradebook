using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.MarketPrices;

public sealed class DeleteMarketPriceEndpoint(IMarketPriceRepository repository)
    : Endpoint<DeleteMarketPriceRequest>
{
    public override void Configure()
    {
        Delete("/api/v1/market-prices/{priceDate}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(DeleteMarketPriceRequest request, CancellationToken ct)
    {
        var outcome = await (
            repository.DeleteAtomicAsync(
                request.PriceDate,
                request.Version,
                request.Reason,
                ActorId.From(User),
                ct
            )
        ).ConfigureAwait(false);
        if (outcome == MutationOutcome.NotFound)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        if (outcome == MutationOutcome.VersionConflict)
        {
            await (
                Send.ResponseAsync(
                    (
                        await (repository.GetByDateAsync(request.PriceDate, ct)).ConfigureAwait(
                            false
                        )
                    )!,
                    409,
                    cancellation: ct
                )
            ).ConfigureAwait(false);
            return;
        }
        await (Send.NoContentAsync(ct)).ConfigureAwait(false);
    }
}
