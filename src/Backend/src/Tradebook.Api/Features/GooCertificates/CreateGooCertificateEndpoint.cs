using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.GooCertificates;

public sealed class CreateGooCertificateEndpoint(IGooCertificateRepository repository)
    : Endpoint<CreateGooCertificateTransactionRequest, GooCertificateTransactionDetailsDto>
{
    public override void Configure()
    {
        Post("/api/v1/goo-certificates");
        Policies("TraderPolicy");
    }

    public override async Task HandleAsync(
        CreateGooCertificateTransactionRequest request,
        CancellationToken ct
    ) =>
        await (
            Send.ResponseAsync(
                await (
                    repository.CreateAtomicAsync(request, ActorId.From(User), ct)
                ).ConfigureAwait(false),
                201,
                cancellation: ct
            )
        ).ConfigureAwait(false);
}
