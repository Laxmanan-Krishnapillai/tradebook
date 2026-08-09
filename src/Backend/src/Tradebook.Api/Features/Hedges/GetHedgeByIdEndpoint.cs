using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Hedges;

public sealed class GetHedgeByIdEndpoint(IHedgeRepository repository)
    : Endpoint<GetHedgeByIdRequest, HedgeDetailsDto>
{
    public override void Configure()
    {
        Get("/api/v1/hedges/{hedgeId}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetHedgeByIdRequest request, CancellationToken ct)
    {
        var result = await (repository.GetByIdAsync(request.HedgeId, ct)).ConfigureAwait(false);
        if (result is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (Send.OkAsync(result, cancellation: ct)).ConfigureAwait(false);
    }
}
