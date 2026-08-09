using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.GooCertificates;

public sealed class RequestGooBatchExportValidator : Validator<RequestGooBatchExportRequest>
{
    public RequestGooBatchExportValidator()
    {
        RuleFor(x => x.GooCertificateTransactionId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
