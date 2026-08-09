using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.GooCertificates;

public sealed class GetGooCertificateHistoryEndpoint(IGooCertificateRepository repository)
    : Endpoint<GetGooCertificateHistoryRequest, GetGooCertificateHistoryResponse>
{
    public override void Configure()
    {
        Get("/api/v1/goo-certificates");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(
        GetGooCertificateHistoryRequest request,
        CancellationToken ct
    ) =>
        await (
            Send.OkAsync(
                await (repository.GetHistoryAsync(request, ct)).ConfigureAwait(false),
                cancellation: ct
            )
        ).ConfigureAwait(false);
}
