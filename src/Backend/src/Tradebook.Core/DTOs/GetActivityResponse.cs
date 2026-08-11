using System.Diagnostics.CodeAnalysis;

namespace Tradebook.Core.DTOs;

public sealed record GetActivityResponse
{
    public GetActivityResponse() { }

    [SetsRequiredMembers]
    public GetActivityResponse(IReadOnlyList<ActivityEntryDto> Items) => this.Items = Items;

    public required IReadOnlyList<ActivityEntryDto> Items { get; init; }
}
