using FastEndpoints;
using FluentValidation;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Events;

public sealed class GetEventsSinceRequest
{
    public long AfterSequence { get; init; }
    public int Limit { get; init; } = 500;
}
