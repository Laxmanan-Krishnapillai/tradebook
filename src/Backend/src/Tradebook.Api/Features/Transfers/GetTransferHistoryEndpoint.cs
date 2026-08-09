using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Transfers;

public sealed class GetTransferHistoryEndpoint(ITransferRepository repository)
    : Endpoint<GetTransferHistoryRequest, GetTransferHistoryResponse>
{
    public override void Configure()
    {
        Get("/api/v1/transfers");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(
        GetTransferHistoryRequest request,
        CancellationToken ct
    ) =>
        await (
            Send.OkAsync(
                await (repository.GetHistoryAsync(request, ct)).ConfigureAwait(false),
                cancellation: ct
            )
        ).ConfigureAwait(false);
}
