using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.GooCertificates;

public sealed class GetGooCertificateByIdEndpoint(IGooCertificateRepository repository)
    : Endpoint<GetGooCertificateByIdRequest, GooCertificateTransactionDetailsDto>
{
    public override void Configure()
    {
        Get("/api/v1/goo-certificates/{gooCertificateTransactionId}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(
        GetGooCertificateByIdRequest request,
        CancellationToken ct
    )
    {
        var result = await (
            repository.GetByIdAsync(request.GooCertificateTransactionId, ct)
        ).ConfigureAwait(false);
        if (result is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (Send.OkAsync(result, cancellation: ct)).ConfigureAwait(false);
    }
}
