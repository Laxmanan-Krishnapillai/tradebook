using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tradebook.UnitTests;

public sealed class ToolingConfigurationTests
{
    [Fact]
    public void Stryker_uses_the_repository_scope_release_build_and_single_80_percent_gate()
    {
        var configPath = FindRepositoryFile("stryker-config.json");
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var config = document.RootElement.GetProperty("stryker-config");

        Assert.Equal("Release", config.GetProperty("configuration").GetString());
        Assert.Equal(80, config.GetProperty("thresholds").GetProperty("break").GetInt32());
        Assert.Equal("src/Backend/Tradebook.sln", config.GetProperty("solution").GetString());
        Assert.Equal("Tradebook.Api.csproj", config.GetProperty("project").GetString());
        Assert.Equal(
            ["tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj"],
            config.GetProperty("test-projects").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(
            ["Features/**/*.cs", "!Features/**/Models/*.cs", "!Features/**/Dto/*.cs"],
            config.GetProperty("mutate").EnumerateArray().Select(item => item.GetString()!).ToArray());
    }

    [Fact]
    public void All_projects_target_net10_and_package_references_have_no_versions_or_overrides()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectFiles = FindProjectFiles(repositoryRoot);

        Assert.Equal(6, projectFiles.Count);
        foreach (var projectFile in projectFiles)
        {
            var project = XDocument.Load(projectFile);
            Assert.Equal(
                ["net10.0"],
                project.Descendants("TargetFramework").Select(element => element.Value).ToArray());
            Assert.Empty(project.Descendants("TargetFrameworks"));

            foreach (var reference in project.Descendants("PackageReference"))
            {
                Assert.Null(reference.Attribute("Version"));
                Assert.Null(reference.Attribute("VersionOverride"));
            }
        }

        var configFiles = projectFiles.Concat(
        [
            Path.Combine(repositoryRoot, "Directory.Build.props"),
            Path.Combine(repositoryRoot, "Directory.Build.targets"),
            Path.Combine(repositoryRoot, "Directory.Packages.props"),
            Path.Combine(repositoryRoot, "global.json"),
            Path.Combine(repositoryRoot, "stryker-config.json"),
            Path.Combine(repositoryRoot, ".github", "workflows", "ci.yml")
        ]);
        Assert.All(configFiles, path => Assert.DoesNotContain("net9.0", File.ReadAllText(path)));
    }

    [Fact]
    public void Central_package_manifest_enables_cpm_and_pins_every_reference_once()
    {
        var expectedVersions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AwesomeAssertions"] = "9.5.0",
            ["Dapper"] = "2.1.79",
            ["FastEndpoints"] = "8.2.0",
            ["FluentValidation"] = "12.1.1",
            ["Microsoft.AspNetCore.Authentication.JwtBearer"] = "10.0.3",
            ["Microsoft.AspNetCore.Mvc.Testing"] = "10.0.3",
            ["Microsoft.AspNetCore.OpenApi"] = "10.0.10",
            ["Microsoft.AspNetCore.SignalR.Client"] = "10.0.3",
            ["Microsoft.AspNetCore.SignalR.Protocols.MessagePack"] = "10.0.10",
            ["Microsoft.Extensions.Caching.Hybrid"] = "10.1.0",
            ["Microsoft.Extensions.Hosting.Abstractions"] = "10.0.9",
            ["Microsoft.NET.Test.Sdk"] = "17.14.1",
            ["Microsoft.OpenApi"] = "2.11.0",
            ["Npgsql"] = "10.0.3",
            ["Respawn"] = "7.0.0",
            ["Testcontainers.PostgreSql"] = "4.6.0",
            ["TngTech.ArchUnitNET.xUnit"] = "0.11.0",
            ["TypeGen"] = "5.0.0",
            ["YamlDotNet"] = "16.3.0",
            ["coverlet.collector"] = "6.0.4",
            ["xunit"] = "2.9.3",
            ["xunit.runner.visualstudio"] = "3.1.0"
        };
        var repositoryRoot = FindRepositoryRoot();
        var manifest = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));

        Assert.Equal("true", manifest.Descendants("ManagePackageVersionsCentrally").Single().Value);
        Assert.Equal("true", manifest.Descendants("CentralPackageTransitivePinningEnabled").Single().Value);

        var packageVersions = manifest.Descendants("PackageVersion").ToArray();
        Assert.Equal(expectedVersions.Count, packageVersions.Length);
        Assert.Equal(
            packageVersions.Length,
            packageVersions.Select(element => element.Attribute("Include")!.Value).Distinct(StringComparer.Ordinal).Count());
        foreach (var expected in expectedVersions)
        {
            var entry = Assert.Single(
                packageVersions,
                element => element.Attribute("Include")?.Value == expected.Key);
            Assert.Equal(expected.Value, entry.Attribute("Version")?.Value);
        }

        var referenceFiles = FindProjectFiles(repositoryRoot)
            .Append(Path.Combine(repositoryRoot, "Directory.Build.props"));
        var referencedPackages = referenceFiles
            .SelectMany(path => XDocument.Load(path).Descendants("PackageReference"))
            .Select(element => element.Attribute("Include")!.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Pinned only to override a vulnerable transitive resolution (NuGetAudit NU1903:
        // https://github.com/advisories/GHSA-v5pm-xwqc-g5wc via Microsoft.AspNetCore.OpenApi).
        // No project references it directly, so it is exempt from the reference check below.
        var transitiveOnlyPins = new[] { "Microsoft.OpenApi" };

        Assert.Empty(referencedPackages.Except(expectedVersions.Keys, StringComparer.Ordinal));
        Assert.Empty(
            expectedVersions.Keys
                .Except(referencedPackages, StringComparer.Ordinal)
                .Except(transitiveOnlyPins, StringComparer.Ordinal));
    }

    [Fact]
    public void Compilation_defaults_are_enabled_only_in_directory_build_props()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sharedProperties = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));

        Assert.Equal("enable", sharedProperties.Descendants("Nullable").Single().Value);
        Assert.Equal("enable", sharedProperties.Descendants("ImplicitUsings").Single().Value);
        foreach (var projectFile in FindProjectFiles(repositoryRoot))
        {
            var project = XDocument.Load(projectFile);
            Assert.Empty(project.Descendants("Nullable"));
            Assert.Empty(project.Descendants("ImplicitUsings"));
        }
    }

    [Fact]
    public void Global_json_pins_dotnet10_with_latest_feature_roll_forward()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("global.json")));
        var sdk = document.RootElement.GetProperty("sdk");

        Assert.Matches("^10\\.0\\.\\d+$", sdk.GetProperty("version").GetString()!);
        Assert.Equal("latestFeature", sdk.GetProperty("rollForward").GetString());
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    [Fact]
    public void Ci_uses_global_json_for_both_dotnet_sdk_installations()
    {
        var workflow = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".github", "workflows", "ci.yml"));

        Assert.Equal(2, Regex.Matches(workflow, @"global-json-file:\s*global\.json").Count);
        Assert.DoesNotContain("dotnet-version:", workflow);
    }

    [Fact]
    public void Global_package_reference_is_reserved_for_task14_without_adding_analyzers()
    {
        var manifestPath = FindRepositoryFile("Directory.Packages.props");
        var manifestText = File.ReadAllText(manifestPath);
        var manifest = XDocument.Parse(manifestText);

        Assert.Contains("Reserved for Task 14: GlobalPackageReference", manifestText);
        Assert.Empty(manifest.Descendants("GlobalPackageReference"));
        Assert.DoesNotContain(
            manifest.Descendants("PackageVersion"),
            element => element.Attribute("Include")!.Value.Contains("Analyzer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Decision_log_records_dotnet10_and_central_package_management()
    {
        var decisionLog = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "docs", "architecture", "decision-log.md"));

        Assert.Contains("D15 — Adopt .NET 10 LTS and NuGet Central Package Management", decisionLog);
        Assert.Contains("CentralPackageTransitivePinningEnabled", decisionLog);
        Assert.Contains("GlobalPackageReference", decisionLog);
    }

    private static string FindRepositoryRoot() =>
        Path.GetDirectoryName(FindRepositoryFile("Directory.Packages.props"))!;

    private static IReadOnlyList<string> FindProjectFiles(string repositoryRoot) =>
        Directory.GetFiles(Path.Combine(repositoryRoot, "src", "Backend", "src"), "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(repositoryRoot, "tests"), "*.csproj", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string FindRepositoryFile(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate repository file '{fileName}'.", fileName);
    }
}
