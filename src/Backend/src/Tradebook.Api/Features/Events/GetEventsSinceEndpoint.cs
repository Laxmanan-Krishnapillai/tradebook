using FastEndpoints;
using FluentValidation;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Events;

public sealed class GetEventsSinceEndpoint(IRealtimeEventReader events)
    : Endpoint<GetEventsSinceRequest, GetEventsSinceResponse>
{
    public override void Configure()
    {
        Get("/api/v1/events");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetEventsSinceRequest request, CancellationToken ct)
    {
        var response = await events
            .GetSinceAsync(request.AfterSequence, request.Limit, ActorId.From(User), ct)
            .ConfigureAwait(false);
        await Send.OkAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
