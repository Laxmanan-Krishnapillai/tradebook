using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Biotickets;

public sealed class UpdateBioticketEndpoint(IBioticketRepository repository)
    : Endpoint<UpdateBioticketRequest, BioticketDetailsDto>
{
    public override void Configure()
    {
        Put("/api/v1/biotickets/{bioticketId}");
        Policies("TraderPolicy");
    }

    public override async Task HandleAsync(UpdateBioticketRequest request, CancellationToken ct)
    {
        var result = await (
            repository.UpdateAtomicAsync(request, ActorId.From(User), ct)
        ).ConfigureAwait(false);
        if (result is not null)
        {
            await (Send.OkAsync(result, cancellation: ct)).ConfigureAwait(false);
            return;
        }
        var current = await (repository.GetByIdAsync(request.BioticketId, ct)).ConfigureAwait(
            false
        );
        if (current is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (Send.ResponseAsync(current, 409, cancellation: ct)).ConfigureAwait(false);
    }
}
