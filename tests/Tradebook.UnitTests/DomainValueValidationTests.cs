using Tradebook.Api.Validation;

namespace Tradebook.UnitTests;

public sealed class DomainValueValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Awaiting")]
    [InlineData("Issue")]
    public void ReportStatusAcceptsOnlyAuthoritativeValues(string? value) =>
        Assert.True(DomainValueValidation.ReportStatus(value));

    [Theory]
    [InlineData("")]
    [InlineData("Deleted")]
    [InlineData("awaiting")]
    public void ReportStatusRejectsUnknownOrNoncanonicalValues(string value) =>
        Assert.False(DomainValueValidation.ReportStatus(value));

    [Theory]
    [InlineData(null)]
    [InlineData("TTF")]
    [InlineData("WITHIN-DAY MKT")]
    public void GasPriceMechanismAcceptsAuthoritativeValues(string? value) =>
        Assert.True(DomainValueValidation.GasPriceMechanism(value));

    [Theory]
    [InlineData("")]
    [InlineData("Fixed")]
    [InlineData("SPOT")]
    public void GasPriceMechanismRejectsUnknownOrNoncanonicalValues(string value) =>
        Assert.False(DomainValueValidation.GasPriceMechanism(value));
}
