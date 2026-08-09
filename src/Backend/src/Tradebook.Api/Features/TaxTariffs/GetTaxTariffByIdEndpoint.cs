using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.TaxTariffs;

public sealed class GetTaxTariffByIdEndpoint(ITaxTariffRepository repository)
    : Endpoint<GetTaxTariffByIdRequest, TaxTariffDetailsDto>
{
    public override void Configure()
    {
        Get("/api/v1/tax-tariffs/{taxTariffId}");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetTaxTariffByIdRequest request, CancellationToken ct)
    {
        var result = await (repository.GetByIdAsync(request.TaxTariffId, ct)).ConfigureAwait(false);
        if (result is null)
        {
            await (Send.NotFoundAsync(ct)).ConfigureAwait(false);
            return;
        }
        await (Send.OkAsync(result, cancellation: ct)).ConfigureAwait(false);
    }
}
