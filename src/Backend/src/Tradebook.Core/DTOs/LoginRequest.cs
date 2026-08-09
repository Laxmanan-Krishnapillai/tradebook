using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record LoginRequest
{
    public LoginRequest() { }

    [SetsRequiredMembers]
    public LoginRequest(string Username, string Password)
    {
        this.Username = Username;
        this.Password = Password;
    }

    public required string Username { get; init; }

    public required string Password { get; init; }
}
