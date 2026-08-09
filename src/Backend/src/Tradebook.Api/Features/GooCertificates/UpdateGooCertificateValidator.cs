using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.GooCertificates;

public sealed class UpdateGooCertificateValidator
    : Validator<UpdateGooCertificateTransactionRequest>
{
    public UpdateGooCertificateValidator()
    {
        RuleFor(x => x.GooCertificateTransactionId).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.Status).Must(GooValidation.Status);
        RuleFor(x => x)
            .Must(x =>
                x.BatchType is not null
                || x.ProducerContractId.HasValue
                || x.CustomerContractId.HasValue
                || x.Register is not null
                || x.Status is not null
                || x.TransactionStartDate.HasValue
                || x.TransactionVolumeMwh.HasValue
                || x.VolumeMwh.HasValue
                || x.Text is not null
            )
            .WithMessage("At least one mutable field is required.");
    }
}
