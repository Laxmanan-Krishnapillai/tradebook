using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tradebook.Api.Options;

namespace Tradebook.UnitTests;

public sealed class NetworkingForwardedHeadersTests
{
    [Fact]
    public void ProductionMissingTrustedProxyCidrIsInvalid()
    {
        var validator = new NetworkingOptionsValidator(CreateEnvironment("Production"));

        var result = validator.Validate(null, new NetworkingOptions());

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            static failure => failure.Contains("required", StringComparison.Ordinal)
        );
    }

    [Theory]
    [InlineData("10.42.0.0/23", true)]
    [InlineData("2001:db8::/32", true)]
    [InlineData("localhost", false)]
    [InlineData("example.com/24", false)]
    [InlineData("10.42.0.0/33", false)]
    public void TrustedProxyCidrParsingIsStrict(string value, bool valid)
    {
        Assert.Equal(valid, NetworkingCidrParser.TryParse(value, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ProductionRequiresNonBlankTrustedProxyCidr(string cidr)
    {
        var validator = new NetworkingOptionsValidator(CreateEnvironment("Production"));

        var result = validator.Validate(null, new NetworkingOptions { TrustedProxyCidr = cidr });

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("10.42.0.0/23")]
    [InlineData("2001:db8::/32")]
    public void ProductionAcceptsValidCidr(string cidr)
    {
        var validator = new NetworkingOptionsValidator(CreateEnvironment("Production"));

        var result = validator.Validate(null, new NetworkingOptions { TrustedProxyCidr = cidr });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("10.42.0.0/33")]
    [InlineData("10.42.0.0")]
    public void ProductionRejectsInvalidCidr(string cidr)
    {
        var validator = new NetworkingOptionsValidator(CreateEnvironment("Production"));

        var result = validator.Validate(null, new NetworkingOptions { TrustedProxyCidr = cidr });

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            static failure => failure.Contains("valid CIDR", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void DevelopmentAllowsAbsentTrustedProxyCidr()
    {
        var validator = new NetworkingOptionsValidator(CreateEnvironment("Development"));

        var result = validator.Validate(null, new NetworkingOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ForwardedHeadersOptionsResolveThroughApplicationServiceRegistration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["Networking:TrustedProxyCidr"] = "10.42.0.0/23",
                }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(CreateEnvironment("Development"));
        services.AddTradebookNetworking();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );

        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            options.ForwardedHeaders
        );
        Assert.Equal(1, options.ForwardLimit);
        Assert.Contains(
            options.KnownIPNetworks,
            network => string.Equals(network.ToString(), "10.42.0.0/23", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void ForwardedHeadersConfigurationRetainsDefaultsAndAddsConfiguredNetwork()
    {
        var options = new ForwardedHeadersOptions();
        var frameworkDefaults = options.KnownIPNetworks.ToArray();

        NetworkingForwardedHeaders.Configure(
            options,
            new NetworkingOptions { TrustedProxyCidr = "10.42.0.0/23" }
        );

        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            options.ForwardedHeaders
        );
        Assert.Equal(1, options.ForwardLimit);
        Assert.Equal(frameworkDefaults.Length + 1, options.KnownIPNetworks.Count);
        Assert.All(
            frameworkDefaults,
            frameworkDefault => Assert.Contains(frameworkDefault, options.KnownIPNetworks)
        );
        Assert.Contains(
            options.KnownIPNetworks,
            network => string.Equals(network.ToString(), "10.42.0.0/23", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void AbsentDevelopmentCidrLeavesOnlyFrameworkDefaults()
    {
        var options = new ForwardedHeadersOptions();
        var frameworkDefaults = options.KnownIPNetworks.ToArray();

        NetworkingForwardedHeaders.Configure(options, new NetworkingOptions());

        Assert.Equal(frameworkDefaults, options.KnownIPNetworks);
        Assert.Equal(
            ForwardedHeaders.None,
            options.ForwardedHeaders & ForwardedHeaders.XForwardedHost
        );
    }

    [Fact]
    public void ForwardedHeadersMiddlewareIsRegisteredBeforeSecurityAndEndpoints()
    {
        var source = ReadProgramSource();
        var forwardingLine = source.IndexOf("app.UseForwardedHeaders();", StringComparison.Ordinal);
        var exceptionLine = source.IndexOf("app.UseExceptionHandler();", StringComparison.Ordinal);
        var authLine = source.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var mapLine = source.IndexOf(
            "app.MapOpenApi().RequireAuthorization();",
            StringComparison.Ordinal
        );
        var noMapLine = source.IndexOf(
            "app.MapTradebookHealthEndpoints();",
            StringComparison.Ordinal
        );

        Assert.True(forwardingLine > -1);
        Assert.True(exceptionLine > -1);
        Assert.True(authLine > -1);
        Assert.True(mapLine > -1 || noMapLine > -1);
        Assert.True(forwardingLine < exceptionLine);
        Assert.True(forwardingLine < authLine);
        Assert.True(forwardingLine < (mapLine > -1 ? mapLine : noMapLine));
    }

    private static StubHostEnvironment CreateEnvironment(string environmentName) =>
        new StubHostEnvironment(environmentName);

    private static string ReadProgramSource()
    {
        return File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "Backend",
                "src",
                "Tradebook.Api",
                "Program.cs"
            )
        );
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

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
