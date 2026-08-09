using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Hedges;

public sealed class GetHedgeHistoryEndpoint(IHedgeRepository repository)
    : Endpoint<GetHedgeHistoryRequest, GetHedgeHistoryResponse>
{
    public override void Configure()
    {
        Get("/api/v1/hedges");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetHedgeHistoryRequest request, CancellationToken ct) =>
        await (
            Send.OkAsync(
                await (repository.GetHistoryAsync(request, ct)).ConfigureAwait(false),
                cancellation: ct
            )
        ).ConfigureAwait(false);
}
