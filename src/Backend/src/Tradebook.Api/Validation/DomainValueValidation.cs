namespace Tradebook.Api.Validation;

internal static class DomainValueValidation
{
    public static bool ReportStatus(string? value) => value is null
        or "Completed - Payment Received/Sent"
        or "In Progress - Invoice Received/Sent"
        or "Pending - No Invoice"
        or "Cancelled"
        or "Awaiting"
        or "Issue";

    public static bool GasPriceMechanism(string? value) => value is null
        or "FIXED"
        or "VARIABLE"
        or "EGSI ETF"
        or "TTF"
        or "WITHIN-DAY MKT"
        or "BGO"
        or "PGO"
        or "THE";
}
