using System.Collections;
using System.Reflection;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Tradebook.Api.Features.Analytics;
using Tradebook.Api.Features.Auth.Login;
using Tradebook.Api.Features.Biotickets;
using Tradebook.Api.Features.CapacityBookings;
using Tradebook.Api.Features.Contracts;
using Tradebook.Api.Features.Dashboards;
using Tradebook.Api.Features.Events;
using Tradebook.Api.Features.GooCertificates;
using Tradebook.Api.Features.Hedges;
using Tradebook.Api.Features.MarketPrices;
using Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;
using Tradebook.Api.Features.PhysicalDeliveries.DeletePhysicalDelivery;
using Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryById;
using Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryHistory;
using Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery;
using Tradebook.Api.Features.TaxTariffs;
using Tradebook.Api.Features.Transfers;

namespace Tradebook.UnitTests;

/// <summary>
/// Pins each endpoint's route, verb, and authorization policy so mutations to
/// Configure() (changed route strings, dropped Policies calls) fail the suite.
/// </summary>
public sealed class EndpointDefinitionTests
{
    [Fact]
    public void Create_delivery_is_POST_deliveries_under_TraderPolicy() =>
        AssertDefinition(Factory.Create<CreatePhysicalDeliveryEndpoint>(new FakeDeliveryRepository(), new FakeCacheService()),
            "POST", "/api/v1/deliveries", "TraderPolicy");

    [Fact]
    public void Update_delivery_is_PUT_deliveries_id_under_TraderPolicy() =>
        AssertDefinition(Factory.Create<UpdatePhysicalDeliveryEndpoint>(new FakeDeliveryRepository(), new FakeCacheService()),
            "PUT", "/api/v1/deliveries/{deliveryId}", "TraderPolicy");

    [Fact]
    public void Delete_delivery_is_DELETE_deliveries_id_under_BackOfficePolicy() =>
        AssertDefinition(Factory.Create<DeletePhysicalDeliveryEndpoint>(new FakeDeliveryRepository(), new FakeCacheService()),
            "DELETE", "/api/v1/deliveries/{deliveryId}", "BackOfficePolicy");

    [Fact]
    public void Delete_hedge_is_DELETE_hedges_id_under_BackOfficePolicy() =>
        AssertDefinition(Factory.Create<DeleteHedgeEndpoint>(default(object)!),
            "DELETE", "/api/v1/hedges/{hedgeId}", "BackOfficePolicy");

    [Fact]
    public void GetById_is_GET_deliveries_id_under_ReadPolicy() =>
        AssertDefinition(Factory.Create<GetDeliveryByIdEndpoint>(new FakeDeliveryRepository(), new FakeCacheService()),
            "GET", "/api/v1/deliveries/{deliveryId}", "ReadPolicy");

    [Fact]
    public void History_is_GET_deliveries_under_ReadPolicy() =>
        AssertDefinition(Factory.Create<GetDeliveryHistoryEndpoint>(new FakeDeliveryRepository()),
            "GET", "/api/v1/deliveries", "ReadPolicy");

    [Fact]
    public void Events_catchup_is_GET_events_under_ReadPolicy() =>
        AssertDefinition(Factory.Create<GetEventsSinceEndpoint>(default(object)!),
            "GET", "/api/v1/events", "ReadPolicy");

    [Fact]
    public void Analytics_query_is_POST_analytics_query_under_ReadPolicy() =>
        AssertDefinition(Factory.Create<AnalyticsQueryEndpoint>(default(object)!, default(object)!),
            "POST", "/api/v1/analytics/query", "ReadPolicy");

    [Fact]
    public void All_domain_endpoint_routes_verbs_and_policies_are_pinned()
    {
        var definitions = new (BaseEndpoint Endpoint, string Verb, string Route, string Policy)[]
        {
            (Factory.Create<CreateContractEndpoint>(default(object)!), "POST", "/api/v1/contracts", "TraderPolicy"),
            (Factory.Create<GetContractByIdEndpoint>(default(object)!), "GET", "/api/v1/contracts/{contractId}", "ReadPolicy"),
            (Factory.Create<GetContractHistoryEndpoint>(default(object)!), "GET", "/api/v1/contracts", "ReadPolicy"),
            (Factory.Create<UpdateContractEndpoint>(default(object)!), "PUT", "/api/v1/contracts/{contractId}", "TraderPolicy"),
            (Factory.Create<DeactivateContractEndpoint>(default(object)!), "DELETE", "/api/v1/contracts/{contractId}", "BackOfficePolicy"),

            (Factory.Create<CreateBioticketEndpoint>(default(object)!), "POST", "/api/v1/biotickets", "TraderPolicy"),
            (Factory.Create<GetBioticketByIdEndpoint>(default(object)!), "GET", "/api/v1/biotickets/{bioticketId}", "ReadPolicy"),
            (Factory.Create<GetBioticketHistoryEndpoint>(default(object)!), "GET", "/api/v1/biotickets", "ReadPolicy"),
            (Factory.Create<UpdateBioticketEndpoint>(default(object)!), "PUT", "/api/v1/biotickets/{bioticketId}", "TraderPolicy"),
            (Factory.Create<CancelBioticketEndpoint>(default(object)!), "DELETE", "/api/v1/biotickets/{bioticketId}", "BackOfficePolicy"),

            (Factory.Create<CreateCapacityBookingEndpoint>(default(object)!), "POST", "/api/v1/capacity-bookings", "TraderPolicy"),
            (Factory.Create<GetCapacityBookingByIdEndpoint>(default(object)!), "GET", "/api/v1/capacity-bookings/{capacityBookingId}", "ReadPolicy"),
            (Factory.Create<GetCapacityBookingHistoryEndpoint>(default(object)!), "GET", "/api/v1/capacity-bookings", "ReadPolicy"),
            (Factory.Create<UpdateCapacityBookingEndpoint>(default(object)!), "PUT", "/api/v1/capacity-bookings/{capacityBookingId}", "TraderPolicy"),
            (Factory.Create<DeleteCapacityBookingEndpoint>(default(object)!), "DELETE", "/api/v1/capacity-bookings/{capacityBookingId}", "BackOfficePolicy"),

            (Factory.Create<CreateTransferEndpoint>(default(object)!), "POST", "/api/v1/transfers", "TraderPolicy"),
            (Factory.Create<GetTransferByIdEndpoint>(default(object)!), "GET", "/api/v1/transfers/{transferId}", "ReadPolicy"),
            (Factory.Create<GetTransferHistoryEndpoint>(default(object)!), "GET", "/api/v1/transfers", "ReadPolicy"),
            (Factory.Create<UpdateTransferEndpoint>(default(object)!), "PUT", "/api/v1/transfers/{transferId}", "TraderPolicy"),
            (Factory.Create<CancelTransferEndpoint>(default(object)!), "DELETE", "/api/v1/transfers/{transferId}", "BackOfficePolicy"),

            (Factory.Create<CreateGooCertificateEndpoint>(default(object)!), "POST", "/api/v1/goo-certificates", "TraderPolicy"),
            (Factory.Create<GetGooCertificateByIdEndpoint>(default(object)!), "GET", "/api/v1/goo-certificates/{gooCertificateTransactionId}", "ReadPolicy"),
            (Factory.Create<GetGooCertificateHistoryEndpoint>(default(object)!), "GET", "/api/v1/goo-certificates", "ReadPolicy"),
            (Factory.Create<UpdateGooCertificateEndpoint>(default(object)!), "PUT", "/api/v1/goo-certificates/{gooCertificateTransactionId}", "TraderPolicy"),
            (Factory.Create<RequestGooBatchExportEndpoint>(default(object)!), "POST", "/api/v1/goo-certificates/{gooCertificateTransactionId}/request-batch-export", "BackOfficePolicy"),
            (Factory.Create<DeleteGooCertificateEndpoint>(default(object)!), "DELETE", "/api/v1/goo-certificates/{gooCertificateTransactionId}", "BackOfficePolicy"),

            (Factory.Create<CreateHedgeEndpoint>(default(object)!), "POST", "/api/v1/hedges", "TraderPolicy"),
            (Factory.Create<GetHedgeByIdEndpoint>(default(object)!), "GET", "/api/v1/hedges/{hedgeId}", "ReadPolicy"),
            (Factory.Create<GetHedgeHistoryEndpoint>(default(object)!), "GET", "/api/v1/hedges", "ReadPolicy"),
            (Factory.Create<UpdateHedgeEndpoint>(default(object)!), "PUT", "/api/v1/hedges/{hedgeId}", "TraderPolicy"),
            (Factory.Create<DeleteHedgeEndpoint>(default(object)!), "DELETE", "/api/v1/hedges/{hedgeId}", "BackOfficePolicy"),

            (Factory.Create<UpsertMarketPriceEndpoint>(default(object)!), "PUT", "/api/v1/market-prices/{priceDate}", "AdminPolicy"),
            (Factory.Create<GetMarketPriceByDateEndpoint>(default(object)!), "GET", "/api/v1/market-prices/{priceDate}", "ReadPolicy"),
            (Factory.Create<GetMarketPriceHistoryEndpoint>(default(object)!), "GET", "/api/v1/market-prices", "ReadPolicy"),
            (Factory.Create<DeleteMarketPriceEndpoint>(default(object)!), "DELETE", "/api/v1/market-prices/{priceDate}", "AdminPolicy"),

            (Factory.Create<CreateTaxTariffEndpoint>(default(object)!), "POST", "/api/v1/tax-tariffs", "AdminPolicy"),
            (Factory.Create<GetTaxTariffByIdEndpoint>(default(object)!), "GET", "/api/v1/tax-tariffs/{taxTariffId}", "ReadPolicy"),
            (Factory.Create<GetTaxTariffHistoryEndpoint>(default(object)!), "GET", "/api/v1/tax-tariffs", "ReadPolicy"),
            (Factory.Create<UpdateTaxTariffEndpoint>(default(object)!), "PUT", "/api/v1/tax-tariffs/{taxTariffId}", "AdminPolicy"),
            (Factory.Create<DeleteTaxTariffEndpoint>(default(object)!), "DELETE", "/api/v1/tax-tariffs/{taxTariffId}", "AdminPolicy"),

            (Factory.Create<GetDashboardEndpoint>(default(object)!), "GET", "/api/v1/dashboards/{dashboardId}", "ReadPolicy"),
            (Factory.Create<SaveDashboardEndpoint>(default(object)!, default(object)!, default(object)!, default(object)!), "PUT", "/api/v1/dashboards/{dashboardId}", "ReadPolicy"),
        };

        foreach (var (endpoint, verb, route, policy) in definitions)
        {
            AssertDefinition(endpoint, verb, route, policy);
        }
    }

    [Fact]
    public void Login_is_anonymous_POST_auth_login()
    {
        var endpoint = Factory.Create<LoginEndpoint>(new FakeUserRepository(), null!);
        Assert.Equal(["/api/v1/auth/login"], endpoint.Definition.Routes!);
        Assert.Equal(["POST"], endpoint.Definition.Verbs!);
        Assert.True(IsAnonymous(endpoint.Definition), "login must stay the sole anonymous API route");
    }

    private static void AssertDefinition(BaseEndpoint endpoint, string verb, string route, string policy)
    {
        Assert.Equal([route], endpoint.Definition.Routes!);
        Assert.Equal([verb], endpoint.Definition.Verbs!);
        Assert.Contains(policy, PoliciesOf(endpoint.Definition));
        Assert.False(IsAnonymous(endpoint.Definition), "endpoint must not allow anonymous access");
    }

    private static IReadOnlyCollection<string> PoliciesOf(EndpointDefinition definition)
    {
        var policies = new List<string>();
        foreach (var member in definition.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!member.Name.Contains("olic", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = member switch
            {
                PropertyInfo property when property.GetIndexParameters().Length == 0 => property.GetValue(definition),
                FieldInfo field => field.GetValue(definition),
                _ => null,
            };
            if (value is IEnumerable enumerable and not string)
            {
                policies.AddRange(enumerable.OfType<string>());
            }
        }

        return policies;
    }

    private static bool IsAnonymous(EndpointDefinition definition)
    {
        foreach (var member in definition.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!member.Name.Contains("nonymous", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = member switch
            {
                PropertyInfo property when property.GetIndexParameters().Length == 0 => property.GetValue(definition),
                FieldInfo field => field.GetValue(definition),
                _ => null,
            };
            if (value is IEnumerable enumerable and not string && enumerable.Cast<object>().Any())
            {
                return true;
            }
        }

        return false;
    }

    private static class Factory
    {
        public static TEndpoint Create<TEndpoint>(params object[] dependencies)
            where TEndpoint : BaseEndpoint =>
            FastEndpoints.Factory.Create<TEndpoint>(
                context => context.AddTestServices(services => services.AddHttpContextAccessor()),
                dependencies);
    }
}
