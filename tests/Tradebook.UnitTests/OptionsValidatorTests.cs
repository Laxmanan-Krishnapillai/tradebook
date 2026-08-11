using System.Text.RegularExpressions;
using Tradebook.Api.AgentTools;
using Tradebook.Infrastructure.Options;

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
            "AddSingleton<IValidateOptions<EntraOptions>, EntraOptionsValidator>()",
            backendSource,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "AddSingleton<IValidateOptions<InAppAgentOptions>, InAppAgentOptionsValidator>()",
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

        Assert.Equal(
            ["DatabaseOptions", "EntraOptions", "InAppAgentOptions", "NetworkingOptions"],
            optionTypes
        );
        AssertValidatorShapes(optionTypes, backendSource);
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

    private static void AssertValidatorShapes(string[] optionTypes, string backendSource)
    {
        foreach (var optionType in optionTypes)
        {
            // Entra and the feature-gated agent use hand-written sealed validators
            // for rules that go beyond attribute validation.
            if (
                string.Equals(optionType, "EntraOptions", StringComparison.Ordinal)
                || string.Equals(optionType, "InAppAgentOptions", StringComparison.Ordinal)
                || string.Equals(optionType, "NetworkingOptions", StringComparison.Ordinal)
            )
            {
                Assert.Matches(
                    $@"internal\s+sealed\s+class\s+{optionType}Validator(?:\s*\([^)]*\))?\s*:\s*IValidateOptions<{optionType}>",
                    backendSource
                );
                continue;
            }

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
}
