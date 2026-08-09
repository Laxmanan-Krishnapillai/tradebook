using FastEndpoints;
using FluentValidation;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Events;

public sealed class GetEventsSinceEndpoint(IOutboxEventReader events)
    : Endpoint<GetEventsSinceRequest, GetEventsSinceResponse>
{
    public override void Configure()
    {
        Get("/api/v1/events");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(GetEventsSinceRequest req, CancellationToken ct)
    {
        var response = await (
            events.GetSinceAsync(req.AfterSequence, req.Limit, ActorId.From(User), ct)
        ).ConfigureAwait(false);
        await (Send.OkAsync(response, cancellation: ct)).ConfigureAwait(false);
    }
}
