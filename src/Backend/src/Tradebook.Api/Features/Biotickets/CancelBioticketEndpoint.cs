using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Biotickets;

public sealed class CancelBioticketEndpoint(IBioticketRepository repository)
    : Endpoint<CancelBioticketRequest>
{
    public override void Configure()
    {
        Delete("/api/v1/biotickets/{bioticketId}");
        Policies("BackOfficePolicy");
    }

    public override async Task HandleAsync(CancelBioticketRequest request, CancellationToken ct)
    {
        var outcome = await (
            repository.CancelAtomicAsync(
                request.BioticketId,
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
                        await (repository.GetByIdAsync(request.BioticketId, ct)).ConfigureAwait(
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
