using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.TaxTariffs;

public sealed record GetTaxTariffByIdRequest
{
    public GetTaxTariffByIdRequest() { }

    [SetsRequiredMembers]
    public GetTaxTariffByIdRequest(Guid TaxTariffId) => this.TaxTariffId = TaxTariffId;

    public required Guid TaxTariffId { get; init; }
}
