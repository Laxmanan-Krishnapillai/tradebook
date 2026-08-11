using Dapper;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Infrastructure.Data;

public static class VogenTypeHandlers
{
    private static int _registered;

    public static void RegisterAll()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0)
            return;

        SqlMapper.AddTypeHandler(new ContractId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new DeliveryId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new CapacityBookingId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new TransferId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new BioticketDeliveryId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new CounterpartyId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new CompanyId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new TradingPointId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new TaxTariffId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new HedgeId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new MarketPriceId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new CapacityPriceIndexId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new GooCertificateTransactionId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new InvoiceLineItemId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new UserId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new DashboardId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new EventId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new AuditLogId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new PriceTypeHandler());
        SqlMapper.AddTypeHandler(new QuantityTypeHandler());
        SqlMapper.AddTypeHandler(new AmountTypeHandler());
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }
}
