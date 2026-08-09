using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.GooCertificates;

public sealed class RequestGooBatchExportEndpoint(IGooCertificateRepository repository)
    : Endpoint<RequestGooBatchExportRequest, GooCertificateTransactionDetailsDto>
{
    public override void Configure()
    {
        Post("/api/v1/goo-certificates/{gooCertificateTransactionId}/request-batch-export");
        Policies("BackOfficePolicy");
    }

    public override async Task HandleAsync(
        RequestGooBatchExportRequest request,
        CancellationToken ct
    )
    {
        var result = await (
            repository.RequestBatchExportAtomicAsync(
                request.GooCertificateTransactionId,
                request.Version,
                ActorId.From(User),
                ct
            )
        ).ConfigureAwait(false);
        if (result is not null)
        {
            await (Send.OkAsync(result, cancellation: ct)).ConfigureAwait(false);
            return;
        }
        var current = await (
            repository.GetByIdAsync(request.GooCertificateTransactionId, ct)
        ).ConfigureAwait(false);
        if (current is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (Send.ResponseAsync(current, 409, cancellation: ct)).ConfigureAwait(false);
    }
}
