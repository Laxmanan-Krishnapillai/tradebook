using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.TaxTariffs;

public sealed class CreateTaxTariffEndpoint(ITaxTariffRepository repository)
    : Endpoint<CreateTaxTariffRequest, TaxTariffDetailsDto>
{
    public override void Configure()
    {
        Post("/api/v1/tax-tariffs");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreateTaxTariffRequest request, CancellationToken ct) =>
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
