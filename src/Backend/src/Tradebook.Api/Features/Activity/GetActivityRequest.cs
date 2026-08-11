namespace Tradebook.Api.Features.Activity;

public sealed record GetActivityRequest
{
    public required string EntityName { get; init; }

    public required string EntityId { get; init; }

    public int PageSize { get; init; } = 100;
}
