using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Transfers;

public sealed class CancelTransferEndpoint(ITransferRepository repository)
    : Endpoint<CancelTransferRequest>
{
    public override void Configure()
    {
        Delete("/api/v1/transfers/{transferId}");
        Policies("BackOfficePolicy");
    }

    public override async Task HandleAsync(CancelTransferRequest request, CancellationToken ct)
    {
        var outcome = await (
            repository.CancelAtomicAsync(
                request.TransferId,
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
                        await (repository.GetByIdAsync(request.TransferId, ct)).ConfigureAwait(
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
