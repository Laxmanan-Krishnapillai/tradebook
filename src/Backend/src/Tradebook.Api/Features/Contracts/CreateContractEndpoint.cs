using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Contracts;

public sealed class CreateContractEndpoint(IContractRepository repository)
    : Endpoint<CreateContractRequest, ContractDetailsDto>
{
    public override void Configure()
    {
        Post("/api/v1/contracts");
        Policies("TraderPolicy");
    }

    public override async Task HandleAsync(CreateContractRequest request, CancellationToken ct) =>
        await (
            Send.ResponseAsync(
                await (
                    repository.CreateAtomicAsync(request, ActorId.From(User), ct)
                ).ConfigureAwait(false),
                201,
                cancellation: ct
            )
        ).ConfigureAwait(false);
}
