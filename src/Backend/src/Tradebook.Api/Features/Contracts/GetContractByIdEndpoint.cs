using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Contracts;

public sealed class GetContractByIdEndpoint(IContractRepository repository)
    : Endpoint<GetContractByIdRequest, ContractDetailsDto>
{
    public override void Configure()
    {
        Get("/api/v1/contracts/{contractId}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetContractByIdRequest request, CancellationToken ct)
    {
        var result = await (repository.GetByIdAsync(request.ContractId, ct)).ConfigureAwait(false);
        if (result is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (Send.OkAsync(result, cancellation: ct)).ConfigureAwait(false);
    }
}
