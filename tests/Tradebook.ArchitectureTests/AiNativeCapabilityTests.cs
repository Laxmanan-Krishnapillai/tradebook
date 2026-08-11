using System.Text.Json;
using System.Xml.Linq;
using Tradebook.Api.AgentTools;
using Tradebook.Api.Features.Analytics;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Tradebook.ArchitectureTests;

public sealed class AiNativeCapabilityTests
{
    private static readonly string[] FrontendDependencySections =
    [
        "dependencies",
        "devDependencies",
    ];

    private static readonly ArchUnitNET.Domain.Architecture Architecture =
        new ArchUnitNET.Loader.ArchLoader()
            .LoadAssemblies(
                typeof(Program).Assembly,
                typeof(Tradebook.Infrastructure.Data.DeliveryRepository).Assembly
            )
            .Build();

    [Fact]
    [Trait("Category", "CapabilityPlane")]
    public void McpAdaptersHaveNoDirectDatabaseOrEndpointTransportDependencies()
    {
        var adapterTypes = Classes().That().ResideInNamespace("Tradebook.Api.AgentTools", true);

        Assert.True(
            adapterTypes
                .Should()
                .NotDependOnAny(Types().That().ResideInNamespace("Tradebook.Infrastructure", true))
                .AndShould()
                .NotDependOnAny(Types().That().ResideInNamespace("Dapper", true))
                .AndShould()
                .NotDependOnAny(Types().That().ResideInNamespace("Npgsql", true))
                .AndShould()
                .NotDependOnAny(Types().That().ResideInNamespace("System.Net.Http", true))
                .AndShould()
                .NotDependOnAny(Types().That().ResideInNamespace("FastEndpoints", true))
                .HasNoViolations(Architecture)
        );
    }

    [Fact]
    [Trait("Category", "CapabilityPlane")]
    public void RestAndMcpAdaptersUseTheSameAnalyticsRunner()
    {
        Assert.Equal(
            [typeof(AnalyticsQueryRunner)],
            typeof(AnalyticsQueryEndpoint)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(p => p.ParameterType)
        );
        Assert.Equal(
            [typeof(AnalyticsQueryRunner)],
            typeof(AnalyticsMcpTools)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(p => p.ParameterType)
        );
    }

    [Fact]
    [Trait("Category", "CapabilityCoverage")]
    public void CapabilityCatalogRoutesExistInTypeSpecAndMcpSdkIsPinned()
    {
        var repositoryRoot = FindRepositoryRoot();
        var typeSpec = File.ReadAllText(
            Path.Combine(repositoryRoot, "docs", "api", "typespec", "main.tsp")
        );

        Assert.All(
            AiCapabilityCatalog.All,
            capability =>
                Assert.Contains(
                    $"@route(\"{capability.RestRoute}\")",
                    typeSpec,
                    StringComparison.Ordinal
                )
        );

        var packages = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        var mcp = Assert.Single(
            packages.Descendants("PackageVersion"),
            element =>
                string.Equals(
                    element.Attribute("Include")?.Value,
                    "ModelContextProtocol.AspNetCore",
                    StringComparison.Ordinal
                )
        );
        Assert.Equal("2.1.0", mcp.Attribute("Version")?.Value);
    }

    [Fact]
    [Trait("Category", "CapabilityCoverage")]
    public void FirstSliceHasNoModelUiRuntimeDependency()
    {
        Assert.All(AiCapabilityCatalog.All, capability => Assert.True(capability.IsReadOnly));

        var packageJson = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "src", "Frontend", "package.json")
        );
        using var document = JsonDocument.Parse(packageJson);
        var dependencyNames = FrontendDependencySections
            .SelectMany(property =>
                document.RootElement.TryGetProperty(property, out var dependencies)
                    ? dependencies.EnumerateObject().Select(dependency => dependency.Name).ToArray()
                    : []
            )
            .ToArray();

        Assert.DoesNotContain(
            dependencyNames,
            name =>
                name.Contains("openui", StringComparison.OrdinalIgnoreCase)
                || name.Contains("json-render", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    [Trait("Category", "CapabilityCoverage")]
    public void InAppAgentStackAndFrontendBridgeAreExactlyPinned()
    {
        var repositoryRoot = FindRepositoryRoot();
        var packages = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        var expectedNuget = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Azure.AI.OpenAI"] = "2.9.0-beta.1",
            ["Azure.Identity"] = "1.21.0",
            ["Microsoft.Agents.AI"] = "1.17.0",
            ["Microsoft.Agents.AI.Hosting.AGUI.AspNetCore"] = "1.17.0-preview.260804.1",
            ["Microsoft.Agents.AI.OpenAI"] = "1.17.0",
        };
        Assert.All(
            expectedNuget,
            expected =>
            {
                var package = Assert.Single(
                    packages.Descendants("PackageVersion"),
                    element =>
                        string.Equals(
                            element.Attribute("Include")?.Value,
                            expected.Key,
                            StringComparison.Ordinal
                        )
                );
                Assert.Equal(expected.Value, package.Attribute("Version")?.Value);
            }
        );

        using var packageJson = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(repositoryRoot, "src", "Frontend", "package.json"))
        );
        var dependencies = packageJson.RootElement.GetProperty("dependencies");
        Assert.Equal("0.0.57", dependencies.GetProperty("@ag-ui/client").GetString());
        Assert.Equal("0.15.13", dependencies.GetProperty("@assistant-ui/react").GetString());
        Assert.Equal("0.0.53", dependencies.GetProperty("@assistant-ui/react-ag-ui").GetString());
        Assert.Equal("5.0.14", dependencies.GetProperty("zustand").GetString());

        var typeSpec = File.ReadAllText(
            Path.Combine(repositoryRoot, "docs", "api", "typespec", "main.tsp")
        );
        Assert.Contains("@route(\"/api/v1/agent/status\")", typeSpec, StringComparison.Ordinal);
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
