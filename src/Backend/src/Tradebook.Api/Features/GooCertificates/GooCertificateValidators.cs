using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.GooCertificates;

internal static class GooValidation
{
    public static bool Status(string? value) => value is null or "Latest transaction" or "Batch export requested" or "Processing" or "Completed" or "Failed";
}

public sealed class CreateGooCertificateValidator : Validator<CreateGooCertificateTransactionRequest>
{
    public CreateGooCertificateValidator()
    {
        RuleFor(x => x.Status).Must(GooValidation.Status);
        RuleFor(x => x.CountryOfProduction).Length(2).When(x => x.CountryOfProduction is not null);
        RuleFor(x => x).Must(x => x.ProducerContractId.HasValue || x.CustomerContractId.HasValue)
            .WithMessage("At least one producer or customer contract is required.");
    }
}

public sealed class UpdateGooCertificateValidator : Validator<UpdateGooCertificateTransactionRequest>
{
    public UpdateGooCertificateValidator()
    {
        RuleFor(x => x.GooCertificateTransactionId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.Status).Must(GooValidation.Status);
        RuleFor(x => x)
            .Must(x => x.BatchType is not null || x.ProducerContractId.HasValue ||
                       x.CustomerContractId.HasValue || x.Register is not null ||
                       x.Status is not null || x.TransactionStartDate.HasValue ||
                       x.TransactionVolumeMwh.HasValue || x.VolumeMwh.HasValue ||
                       x.Text is not null)
            .WithMessage("At least one mutable field is required.");
    }
}

public sealed class RequestGooBatchExportValidator : Validator<RequestGooBatchExportRequest>
{
    public RequestGooBatchExportValidator()
    {
        RuleFor(x => x.GooCertificateTransactionId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}

public sealed class DeleteGooCertificateValidator : Validator<DeleteGooCertificateTransactionRequest>
{
    public DeleteGooCertificateValidator()
    {
        RuleFor(x => x.GooCertificateTransactionId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
