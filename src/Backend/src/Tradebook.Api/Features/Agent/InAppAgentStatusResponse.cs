namespace Tradebook.Api.Features.Agent;

public sealed record InAppAgentStatusResponse(
    bool Enabled,
    bool ReadOnly,
    string Transport,
    string RunPath
);
