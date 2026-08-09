using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Contracts;

public sealed class DeactivateContractEndpoint(IContractRepository repository)
    : Endpoint<DeactivateContractRequest>
{
    public override void Configure()
    {
        Delete("/api/v1/contracts/{contractId}");
        Policies("BackOfficePolicy");
    }

    public override async Task HandleAsync(DeactivateContractRequest request, CancellationToken ct)
    {
        var outcome = await (
            repository.DeactivateAtomicAsync(
                request.ContractId,
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
                        await (repository.GetByIdAsync(request.ContractId, ct)).ConfigureAwait(
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
