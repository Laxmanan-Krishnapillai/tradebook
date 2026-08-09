using FastEndpoints;
using FluentValidation;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Events;

public sealed class GetEventsSinceValidator : Validator<GetEventsSinceRequest>
{
    public GetEventsSinceValidator()
    {
        RuleFor(request => request.AfterSequence).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Limit).InclusiveBetween(1, 500);
    }
}
