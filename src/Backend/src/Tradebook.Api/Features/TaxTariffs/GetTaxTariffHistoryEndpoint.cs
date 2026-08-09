using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.TaxTariffs;

public sealed class GetTaxTariffHistoryEndpoint(ITaxTariffRepository repository)
    : Endpoint<GetTaxTariffHistoryRequest, GetTaxTariffHistoryResponse>
{
    public override void Configure()
    {
        Get("/api/v1/tax-tariffs");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(
        GetTaxTariffHistoryRequest request,
        CancellationToken ct
    ) =>
        await (
            Send.OkAsync(
                await (repository.GetHistoryAsync(request, ct)).ConfigureAwait(false),
                cancellation: ct
            )
        ).ConfigureAwait(false);
}
