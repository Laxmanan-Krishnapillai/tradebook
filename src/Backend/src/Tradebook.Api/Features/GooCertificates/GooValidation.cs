using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.GooCertificates;

internal static class GooValidation
{
    public static bool Status(string? value) =>
        value
            is null
                or "Latest transaction"
                or "Batch export requested"
                or "Processing"
                or "Completed"
                or "Failed";
}
