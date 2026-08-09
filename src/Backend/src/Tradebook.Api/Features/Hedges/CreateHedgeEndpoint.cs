using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Hedges;

public sealed class CreateHedgeEndpoint(IHedgeRepository repository)
    : Endpoint<CreateHedgeRequest, HedgeDetailsDto>
{
    public override void Configure()
    {
        Post("/api/v1/hedges");
        Policies("TraderPolicy");
    }

    public override async Task HandleAsync(CreateHedgeRequest request, CancellationToken ct) =>
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
