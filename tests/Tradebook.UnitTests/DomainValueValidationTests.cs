using Tradebook.Api.Validation;

namespace Tradebook.UnitTests;

public sealed class DomainValueValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Awaiting")]
    [InlineData("Issue")]
    public void Report_status_accepts_only_authoritative_values(string? value) =>
        Assert.True(DomainValueValidation.ReportStatus(value));

    [Theory]
    [InlineData("")]
    [InlineData("Deleted")]
    [InlineData("awaiting")]
    public void Report_status_rejects_unknown_or_noncanonical_values(string value) =>
        Assert.False(DomainValueValidation.ReportStatus(value));

    [Theory]
    [InlineData(null)]
    [InlineData("TTF")]
    [InlineData("WITHIN-DAY MKT")]
    public void Gas_price_mechanism_accepts_authoritative_values(string? value) =>
        Assert.True(DomainValueValidation.GasPriceMechanism(value));

    [Theory]
    [InlineData("")]
    [InlineData("Fixed")]
    [InlineData("SPOT")]
    public void Gas_price_mechanism_rejects_unknown_or_noncanonical_values(string value) =>
        Assert.False(DomainValueValidation.GasPriceMechanism(value));
}
