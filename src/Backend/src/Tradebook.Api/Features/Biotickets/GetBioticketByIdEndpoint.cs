using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Biotickets;

public sealed class GetBioticketByIdEndpoint(IBioticketRepository repository)
    : Endpoint<GetBioticketByIdRequest, BioticketDetailsDto>
{
    public override void Configure()
    {
        Get("/api/v1/biotickets/{bioticketId}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetBioticketByIdRequest request, CancellationToken ct)
    {
        var result = await (repository.GetByIdAsync(request.BioticketId, ct)).ConfigureAwait(false);
        if (result is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (Send.OkAsync(result, cancellation: ct)).ConfigureAwait(false);
    }
}
