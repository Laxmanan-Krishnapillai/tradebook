using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.TaxTariffs;

public sealed class DeleteTaxTariffEndpoint(ITaxTariffRepository repository)
    : Endpoint<DeleteTaxTariffRequest>
{
    public override void Configure()
    {
        Delete("/api/v1/tax-tariffs/{taxTariffId}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(DeleteTaxTariffRequest request, CancellationToken ct)
    {
        var outcome = await (
            repository.DeleteAtomicAsync(
                request.TaxTariffId,
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
                        await (repository.GetByIdAsync(request.TaxTariffId, ct)).ConfigureAwait(
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
