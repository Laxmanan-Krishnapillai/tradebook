using Microsoft.Extensions.DependencyInjection;
using Tradebook.Core.Interfaces;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Migrations;

namespace Tradebook.Infrastructure.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddTradebookPersistence(this IServiceCollection services)
    {
        services.AddSingleton<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<ICapacityBookingRepository, CapacityBookingRepository>();
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<IBioticketRepository, BioticketRepository>();
        services.AddScoped<IGooCertificateRepository, GooCertificateRepository>();
        services.AddScoped<IMarketPriceRepository, MarketPriceRepository>();
        services.AddScoped<ITaxTariffRepository, TaxTariffRepository>();
        services.AddScoped<IHedgeRepository, HedgeRepository>();
        services.AddSingleton<DatabaseMigrator>();
        return services;
    }
}
