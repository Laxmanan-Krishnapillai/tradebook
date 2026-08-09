using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Transfers;

public sealed class GetTransferByIdEndpoint(ITransferRepository repository)
    : Endpoint<GetTransferByIdRequest, TransferDetailsDto>
{
    public override void Configure()
    {
        Get("/api/v1/transfers/{transferId}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetTransferByIdRequest request, CancellationToken ct)
    {
        var result = await (repository.GetByIdAsync(request.TransferId, ct)).ConfigureAwait(false);
        if (result is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (Send.OkAsync(result, cancellation: ct)).ConfigureAwait(false);
    }
}
