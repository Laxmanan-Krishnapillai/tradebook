using Microsoft.Extensions.Options;
using Tradebook.Core.DTOs;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Options;

namespace Tradebook.IntegrationTests;

public sealed class HistoryFilterNullabilityIntegrationTests(PostgresTestFixture postgres)
    : PostgresDatabaseTestBase(postgres)
{
    // Regression for 42P08 "could not determine data type of parameter": every history query
    // filters via (@Param IS NULL OR column = @Param), and Npgsql sends nulls untyped, so each
    // parameter's first occurrence needs an explicit ::type cast for Postgres to prepare the
    // statement. The default request (all filters null) is exactly what the UI list pages send.
    [Fact]
    public async Task EveryHistoryQueryExecutesWithAllFiltersNull()
    {
        var connections = new NpgsqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = Postgres.ConnectionString })
        );
        await using var configuredConnections = connections.ConfigureAwait(true);
        var publisher = new RecordingTransactionalEventPublisher();

        var biotickets = await new BioticketRepository(connections, publisher)
            .GetHistoryAsync(new GetBioticketHistoryRequest(), CancellationToken.None)
            .ConfigureAwait(true);
        Assert.NotNull(biotickets);

        var capacityBookings = await new CapacityBookingRepository(connections, publisher)
            .GetHistoryAsync(new GetCapacityBookingHistoryRequest(), CancellationToken.None)
            .ConfigureAwait(true);
        Assert.NotNull(capacityBookings);

        var contracts = await new ContractRepository(connections, publisher)
            .GetHistoryAsync(new GetContractHistoryRequest(), CancellationToken.None)
            .ConfigureAwait(true);
        Assert.NotNull(contracts);

        var deliveries = await new DeliveryRepository(connections, publisher)
            .GetHistoryAsync(new GetDeliveryHistoryRequest(), CancellationToken.None)
            .ConfigureAwait(true);
        Assert.NotNull(deliveries);

        var gooCertificates = await new GooCertificateRepository(connections, publisher)
            .GetHistoryAsync(new GetGooCertificateHistoryRequest(), CancellationToken.None)
            .ConfigureAwait(true);
        Assert.NotNull(gooCertificates);

        var hedges = await new HedgeRepository(connections, publisher)
            .GetHistoryAsync(new GetHedgeHistoryRequest(), CancellationToken.None)
            .ConfigureAwait(true);
        Assert.NotNull(hedges);

        var marketPrices = await new MarketPriceRepository(connections, publisher)
            .GetHistoryAsync(new GetMarketPriceHistoryRequest(), CancellationToken.None)
            .ConfigureAwait(true);
        Assert.NotNull(marketPrices);

        var taxTariffs = await new TaxTariffRepository(connections, publisher)
            .GetHistoryAsync(new GetTaxTariffHistoryRequest(), CancellationToken.None)
            .ConfigureAwait(true);
        Assert.NotNull(taxTariffs);

        var transfers = await new TransferRepository(connections, publisher)
            .GetHistoryAsync(new GetTransferHistoryRequest(), CancellationToken.None)
            .ConfigureAwait(true);
        Assert.NotNull(transfers);
    }
}
