using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Development;
using Tradebook.Infrastructure.Options;

namespace Tradebook.IntegrationTests;

public sealed class DevelopmentDataSeederIntegrationTests(PostgresTestFixture fixture)
    : PostgresDatabaseTestBase(fixture)
{
    [Fact]
    public async Task EmptyDatabaseIsSeededDeterministicallyAndOnlyOnce()
    {
        await using var connections = new NpgsqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = Postgres.ConnectionString })
        );
        var seeder = new DevelopmentDataSeeder(
            connections,
            NullLogger<DevelopmentDataSeeder>.Instance
        );

        var firstRun = await seeder.SeedIfEmptyAsync(TestContext.Current.CancellationToken);
        var secondRun = await seeder.SeedIfEmptyAsync(TestContext.Current.CancellationToken);

        Assert.True(firstRun);
        Assert.False(secondRun);
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        var counts = await connection.QuerySingleAsync<SeedCounts>(
            """
            SELECT
                (SELECT COUNT(*) FROM contracts) AS ContractCount,
                (SELECT COUNT(*) FROM physical_deliveries) AS DeliveryCount,
                (SELECT COUNT(*) FROM capacity_bookings) AS CapacityBookingCount,
                (SELECT COUNT(*) FROM transfers) AS TransferCount,
                (SELECT COUNT(*) FROM bioticket_deliveries) AS BioticketCount,
                (SELECT COUNT(*) FROM market_prices) AS MarketPriceCount,
                (SELECT COUNT(*) FROM goo_certificate_transactions) AS CertificateTransactionCount
            """
        );
        var deterministicContractId = new Guid(
            SHA256
                .HashData(Encoding.UTF8.GetBytes("tradebook-development-seed-v1:contract:1"))
                .AsSpan(0, 16)
        );
        var firstContract = await connection.QuerySingleAsync<string>(
            "SELECT contract_name FROM contracts WHERE id = @Id",
            new { Id = deterministicContractId }
        );
        var invalidMoneyScale = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*) FROM market_prices
            WHERE eur_sek <> round(eur_sek, 4) OR eur_chf <> round(eur_chf, 4)
               OR eur_gbp <> round(eur_gbp, 4) OR eur_usd <> round(eur_usd, 4)
               OR eur_dkk <> round(eur_dkk, 4)
            """
        );

        Assert.Equal(new SeedCounts(24, 48, 10, 10, 16, 180, 12), counts);
        Assert.Equal("VATT.46.BG.2601.NOQS", firstContract);
        Assert.Equal(0, invalidMoneyScale);
    }

    private sealed record SeedCounts(
        long ContractCount,
        long DeliveryCount,
        long CapacityBookingCount,
        long TransferCount,
        long BioticketCount,
        long MarketPriceCount,
        long CertificateTransactionCount
    );
}
