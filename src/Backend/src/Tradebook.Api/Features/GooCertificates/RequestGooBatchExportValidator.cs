using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.GooCertificates;

public sealed class RequestGooBatchExportValidator : Validator<RequestGooBatchExportRequest>
{
    public RequestGooBatchExportValidator()
    {
        RuleFor(x => x.GooCertificateTransactionId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
