using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record ProblemDetailsResponse(string Type, string Title, int Status, string? Detail);
