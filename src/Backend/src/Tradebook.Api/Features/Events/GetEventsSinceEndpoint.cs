using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;
using Tradebook.Api.Security;

namespace Tradebook.Api.Features.Events;

public sealed class GetEventsSinceRequest
{
    public long AfterSequence { get; init; }
    public int Limit { get; init; } = 500;
}

public sealed class GetEventsSinceValidator : Validator<GetEventsSinceRequest>
{
    public GetEventsSinceValidator()
    {
        RuleFor(request => request.AfterSequence)
            .GreaterThanOrEqualTo(0);
        RuleFor(request => request.Limit)
            .InclusiveBetween(1, 500);
    }
}

public sealed class GetEventsSinceEndpoint(IRealtimeEventReader events)
    : Endpoint<GetEventsSinceRequest, GetEventsSinceResponse>
{
    public override void Configure()
    {
        Get("/api/v1/events");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(
        GetEventsSinceRequest request,
        CancellationToken cancellationToken)
    {
        var response = await events.GetSinceAsync(
            request.AfterSequence,
            request.Limit,
            ActorId.From(User),
            cancellationToken);
        await Send.OkAsync(response, cancellation: cancellationToken);
    }
}
