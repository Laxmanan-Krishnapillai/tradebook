using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Hedges;

public sealed class DeleteHedgeEndpoint(IHedgeRepository repository) : Endpoint<DeleteHedgeRequest>
{
    public override void Configure()
    {
        Delete("/api/v1/hedges/{hedgeId}");
        Policies("BackOfficePolicy");
    }

    public override async Task HandleAsync(DeleteHedgeRequest request, CancellationToken ct)
    {
        var outcome = await (
            repository.DeleteAtomicAsync(
                request.HedgeId,
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
                    (await (repository.GetByIdAsync(request.HedgeId, ct)).ConfigureAwait(false))!,
                    409,
                    cancellation: ct
                )
            ).ConfigureAwait(false);
            return;
        }
        await (Send.NoContentAsync(ct)).ConfigureAwait(false);
    }
}
