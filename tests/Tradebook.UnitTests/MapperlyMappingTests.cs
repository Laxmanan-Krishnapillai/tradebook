using System.Text.Json;
using Tradebook.Api.Features.Auth.Login;
using Tradebook.Api.Features.Dashboards;
using Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;
using Tradebook.Core.Domain.Entities;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class MapperlyMappingTests
{
    private static readonly (string RelativePath, string ClassName)[] ExpectedMapperSources =
    [
        ("Tradebook.Api/Features/Auth/Login/LoginMapper.cs", "LoginMapper"),
        ("Tradebook.Api/Features/Dashboards/DashboardMapper.cs", "DashboardMapper"),
        (
            "Tradebook.Api/Features/PhysicalDeliveries/CreatePhysicalDelivery/PhysicalDeliveryMapper.cs",
            "PhysicalDeliveryMapper"
        ),
        ("Tradebook.Infrastructure/Data/DeliveryMapper.cs", "DeliveryMapper"),
        ("Tradebook.Infrastructure/Outbox/OutboxEventMapper.cs", "OutboxEventMapper"),
    ];

    [Fact]
    public void SAFE05BackendMappersAreExactlyTheFiveMapperlyPartialTypes()
    {
        var backendSourceRoot = Path.Combine(FindRepositoryRoot(), "src", "Backend", "src");
        var actualFiles = Directory
            .EnumerateFiles(backendSourceRoot, "*Mapper.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Select(path => Path.GetRelativePath(backendSourceRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ExpectedMapperSources
                .Select(mapper => mapper.RelativePath)
                .Order(StringComparer.Ordinal),
            actualFiles,
            StringComparer.Ordinal
        );
        foreach (var (relativePath, className) in ExpectedMapperSources)
        {
            var source = File.ReadAllText(Path.Combine(backendSourceRoot, relativePath));
            Assert.Contains("[Mapper", source, StringComparison.Ordinal);
            Assert.Contains($"partial class {className}", source, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Reflection", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Activator.CreateInstance", source, StringComparison.Ordinal);
            Assert.DoesNotContain("GetProperties(", source, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"\bdynamic\b", source);
        }
    }

    [Fact]
    public void LoginMapperCombinesUserTokenAndExpiryAndRenamesActorId()
    {
        var actorId = Guid.NewGuid();
        var expiresAt = new DateTimeOffset(2026, 8, 9, 18, 0, 0, TimeSpan.Zero);
        var user = new User
        {
            Id = actorId,
            Username = "trader",
            PasswordHash = "hash",
            Roles = ["Trader"],
            IsActive = true,
        };

        var response = LoginMapper.ToResponse(user, "signed-token", expiresAt);

        Assert.Equal(actorId, response.ActorId);
        Assert.Equal("signed-token", response.AccessToken);
        Assert.Equal(expiresAt, response.ExpiresAtUtc);
    }

    [Fact]
    public void PhysicalDeliveryMapperProjectsOnlyTheCreateResponseContract()
    {
        var deliveryId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 8, 9, 10, 15, 0, TimeSpan.Zero);
        var details = new PhysicalDeliveryDetailsDto
        {
            DeliveryId = deliveryId,
            ContractId = Guid.NewGuid(),
            ContractInstanceId = "NRGD.49.GAS.THE-8-2026",
            BookType = "Sales",
            SupplyMonth = new DateOnly(2026, 8, 1),
            InvoiceAmountEur = 123.45m,
            Status = "Awaiting",
            Version = 3,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddMinutes(1),
        };

        var response = PhysicalDeliveryMapper.ToResponse(details);

        Assert.Equal(deliveryId, response.DeliveryId);
        Assert.Equal(details.ContractInstanceId, response.ContractInstanceId);
        Assert.Equal(details.InvoiceAmountEur, response.InvoiceAmountEur);
        Assert.Equal(details.Status, response.Status);
        Assert.Equal(details.Version, response.Version);
        Assert.Equal(createdAt, response.CreatedAt);
    }

    [Fact]
    public void DashboardMapperCombinesTheRouteIdAndParsesTheStoredJson()
    {
        var dashboardId = Guid.NewGuid();
        var row = new DashboardRow("{\"version\":4,\"widgets\":[]}", 4);

        var response = DashboardMapper.ToResponse(row, dashboardId);

        Assert.Equal(dashboardId, response.DashboardId);
        Assert.Equal(4, response.Version);
        Assert.Equal(JsonValueKind.Object, response.Layout.ValueKind);
        Assert.Equal(4, response.Layout.GetProperty("version").GetInt64());
        Assert.Equal(0, response.Layout.GetProperty("widgets").GetArrayLength());
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
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
