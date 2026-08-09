using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Biotickets;

public sealed class CreateBioticketEndpoint(IBioticketRepository repository)
    : Endpoint<CreateBioticketRequest, BioticketDetailsDto>
{
    public override void Configure()
    {
        Post("/api/v1/biotickets");
        Policies("TraderPolicy");
    }

    public override async Task HandleAsync(CreateBioticketRequest request, CancellationToken ct) =>
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
