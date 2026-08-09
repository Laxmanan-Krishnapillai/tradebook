using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Transfers;

public sealed class CreateTransferEndpoint(ITransferRepository repository)
    : Endpoint<CreateTransferRequest, TransferDetailsDto>
{
    public override void Configure()
    {
        Post("/api/v1/transfers");
        Policies("TraderPolicy");
    }

    public override async Task HandleAsync(CreateTransferRequest request, CancellationToken ct) =>
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
