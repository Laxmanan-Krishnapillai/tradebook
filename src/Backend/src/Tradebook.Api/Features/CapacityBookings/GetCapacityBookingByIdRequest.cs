using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.CapacityBookings;

public sealed record GetCapacityBookingByIdRequest
{
    public GetCapacityBookingByIdRequest() { }

    [SetsRequiredMembers]
    public GetCapacityBookingByIdRequest(Guid CapacityBookingId) =>
        this.CapacityBookingId = CapacityBookingId;

    public required Guid CapacityBookingId { get; init; }
}
