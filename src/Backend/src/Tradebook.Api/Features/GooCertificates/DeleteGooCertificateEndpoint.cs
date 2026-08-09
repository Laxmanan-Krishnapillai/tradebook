using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.GooCertificates;

public sealed class DeleteGooCertificateEndpoint(IGooCertificateRepository repository)
    : Endpoint<DeleteGooCertificateTransactionRequest>
{
    public override void Configure()
    {
        Delete("/api/v1/goo-certificates/{gooCertificateTransactionId}");
        Policies("BackOfficePolicy");
    }

    public override async Task HandleAsync(
        DeleteGooCertificateTransactionRequest request,
        CancellationToken ct
    )
    {
        var outcome = await (
            repository.DeleteAtomicAsync(
                request.GooCertificateTransactionId,
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
                        await (
                            repository.GetByIdAsync(request.GooCertificateTransactionId, ct)
                        ).ConfigureAwait(false)
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
