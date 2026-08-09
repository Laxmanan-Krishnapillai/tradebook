using System.Security.Claims;
using FastEndpoints;
using Tradebook.Api.Features.Events;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class GetEventsSinceEndpointMutationTests
{
    [Theory]
    [InlineData(0L, 1)]
    [InlineData(long.MaxValue, 500)]
    public void ValidatorAcceptsInclusiveSequenceAndLimitBoundaries(long afterSequence, int limit)
    {
        var result = new GetEventsSinceValidator().Validate(
            new GetEventsSinceRequest { AfterSequence = afterSequence, Limit = limit }
        );

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(-1L, 1, nameof(GetEventsSinceRequest.AfterSequence))]
    [InlineData(0L, 0, nameof(GetEventsSinceRequest.Limit))]
    [InlineData(0L, 501, nameof(GetEventsSinceRequest.Limit))]
    public void ValidatorRejectsEachNeighborOutsideTheAllowedBounds(
        long afterSequence,
        int limit,
        string propertyName
    )
    {
        var result = new GetEventsSinceValidator().Validate(
            new GetEventsSinceRequest { AfterSequence = afterSequence, Limit = limit }
        );

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => string.Equals(error.PropertyName, propertyName, StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task HandlerForwardsTheExactCursorLimitActorAndCancellationToken()
    {
        var actorId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var response = new GetEventsSinceResponse(
            [
                new EntityChangedEventDto(
                    Guid.NewGuid(),
                    42,
                    "WorkspaceDashboard",
                    Guid.NewGuid().ToString(),
                    "Updated",
                    "{\"version\":2}"
                ),
            ],
            42
        );
        var reader = new RecordingOutboxEventReader(response);
        var endpoint = Factory.Create<GetEventsSinceEndpoint>(
            context => context.User = Principal(actorId),
            reader
        );
        var request = new GetEventsSinceRequest { AfterSequence = 17, Limit = 23 };

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(response, endpoint.Response);
        Assert.Equal(1, reader.Calls);
        Assert.Equal(17, reader.AfterSequence);
        Assert.Equal(23, reader.Limit);
        Assert.Equal(actorId, reader.ActorId);
        Assert.Equal(cancellation.Token, reader.CancellationToken);
    }

    private static ClaimsPrincipal Principal(Guid actorId) =>
        new(
            new ClaimsIdentity(
                [
                    new Claim("oid", actorId.ToString()),
                    new("tid", "11111111-1111-1111-1111-111111111111"),
                    new("tradebook_tenant", "11111111-1111-1111-1111-111111111111"),
                ],
                "test"
            )
        );

    private sealed class RecordingOutboxEventReader(GetEventsSinceResponse response)
        : IOutboxEventReader
    {
        public int Calls { get; private set; }
        public long AfterSequence { get; private set; }
        public int Limit { get; private set; }
        public Guid ActorId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<GetEventsSinceResponse> GetSinceAsync(
            long afterSequence,
            int limit,
            Guid actorId,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            AfterSequence = afterSequence;
            Limit = limit;
            ActorId = actorId;
            CancellationToken = cancellationToken;
            return Task.FromResult(response);
        }
    }
}
