using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Biotickets;

public sealed class GetBioticketHistoryEndpoint(IBioticketRepository repository)
    : Endpoint<GetBioticketHistoryRequest, GetBioticketHistoryResponse>
{
    public override void Configure()
    {
        Get("/api/v1/biotickets");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(
        GetBioticketHistoryRequest request,
        CancellationToken ct
    ) =>
        await (
            Send.OkAsync(
                await (repository.GetHistoryAsync(request, ct)).ConfigureAwait(false),
                cancellation: ct
            )
        ).ConfigureAwait(false);
}
