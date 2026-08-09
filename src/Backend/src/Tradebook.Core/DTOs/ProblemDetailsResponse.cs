using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record ProblemDetailsResponse
{
    public ProblemDetailsResponse() { }

    [SetsRequiredMembers]
    public ProblemDetailsResponse(string Type, string Title, int Status, string? Detail)
    {
        this.Type = Type;
        this.Title = Title;
        this.Status = Status;
        this.Detail = Detail;
    }

    public required string Type { get; init; }

    public required string Title { get; init; }

    public required int Status { get; init; }

    public string? Detail { get; init; }
}
