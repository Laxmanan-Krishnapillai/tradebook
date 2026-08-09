using System.Text.RegularExpressions;
using Tradebook.Infrastructure.Options;
using Tradebook.Infrastructure.Outbox;

namespace Tradebook.UnitTests;

public sealed class OptionsValidatorTests
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    public void SAFE0304AllOptionsUseGeneratedStartupValidationWithoutReflection()
    {
        var repositoryRoot = FindRepositoryRoot();
        var backendSourceRoot = Path.Combine(repositoryRoot, "src", "Backend", "src");
        var sourceFiles = Directory
            .EnumerateFiles(backendSourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
                && !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToArray();

        Assert.DoesNotContain(
            sourceFiles,
            path =>
                File.ReadAllText(path)
                    .Contains("ValidateDataAnnotations(", StringComparison.Ordinal)
        );
        var backendSource = string.Join(Environment.NewLine, sourceFiles.Select(File.ReadAllText));
        Assert.Contains(
            "AddSingleton<IValidateOptions<JwtOptions>, JwtSecurityOptionsValidator>()",
            backendSource,
            StringComparison.Ordinal
        );
        var optionTypes = Regex
            .Matches(
                backendSource,
                @"\bpublic\s+sealed\s+(?:class|record)\s+(?<name>\w+Options)\b",
                RegexOptions.None,
                RegexTimeout
            )
            .Select(match => match.Groups["name"].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["DatabaseOptions", "JwtOptions", "OutboxOptions"], optionTypes);
        foreach (var optionType in optionTypes)
        {
            Assert.Matches(
                $@"\[OptionsValidator\]\s+(?:public|internal)\s+sealed\s+partial\s+class\s+{optionType}Validator\b",
                backendSource
            );
            Assert.Contains(
                $"AddSingleton<IValidateOptions<{optionType}>, {optionType}Validator>()",
                backendSource,
                StringComparison.Ordinal
            );
            Assert.Matches(
                $@"(?s)AddOptions<{optionType}>\(\)(?:(?!;).)*?ValidateOnStart\(\);",
                backendSource
            );
        }
    }

    [Fact]
    public void DatabaseConnectionStringIsRequired()
    {
        var result = new DatabaseOptionsValidator().Validate(null, new DatabaseOptions());

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("ConnectionString", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void ConfiguredDatabaseOptionsAreAccepted()
    {
        var result = new DatabaseOptionsValidator().Validate(
            null,
            new DatabaseOptions { ConnectionString = "Host=localhost;Database=tradebook" }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(101, 1, 2)]
    [InlineData(100, 0, 2)]
    [InlineData(100, 301, 2)]
    [InlineData(100, 1, 0)]
    [InlineData(100, 1, 301)]
    public void OutboxValuesOutsideTheSupportedRangesAreRejected(
        int batchSize,
        int fallbackPollSeconds,
        int errorBackoffSeconds
    )
    {
        var result = new OutboxOptionsValidator().Validate(
            null,
            new OutboxOptions
            {
                BatchSize = batchSize,
                FallbackPollSeconds = fallbackPollSeconds,
                ErrorBackoffSeconds = errorBackoffSeconds,
            }
        );

        Assert.True(result.Failed);
    }

    [Fact]
    public void DefaultOutboxOptionsAreAccepted()
    {
        var result = new OutboxOptionsValidator().Validate(null, new OutboxOptions());

        Assert.True(result.Succeeded);
    }

    private static string FindRepositoryRoot()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
