using System.Collections;
using System.Reflection;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Tradebook.Api.Features.Activity;
using Tradebook.Api.Features.Agent;
using Tradebook.Api.Features.Analytics;
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
    public void CreateDeliveryIsPOSTDeliveriesUnderTraderPolicy() =>
        AssertDefinition(
            Factory.Create<CreatePhysicalDeliveryEndpoint>(
                new FakeDeliveryRepository(),
                new FakeCacheService()
            ),
            "POST",
            "/api/v1/deliveries",
            "TraderPolicy"
        );

    [Fact]
    public void UpdateDeliveryIsPUTDeliveriesIdUnderTraderPolicy() =>
        AssertDefinition(
            Factory.Create<UpdatePhysicalDeliveryEndpoint>(
                new FakeDeliveryRepository(),
                new FakeCacheService()
            ),
            "PUT",
            "/api/v1/deliveries/{deliveryId}",
            "TraderPolicy"
        );

    [Fact]
    public void DeleteDeliveryIsDELETEDeliveriesIdUnderBackOfficePolicy() =>
        AssertDefinition(
            Factory.Create<DeletePhysicalDeliveryEndpoint>(
                new FakeDeliveryRepository(),
                new FakeCacheService()
            ),
            "DELETE",
            "/api/v1/deliveries/{deliveryId}",
            "BackOfficePolicy"
        );

    [Fact]
    public void DeleteHedgeIsDELETEHedgesIdUnderBackOfficePolicy() =>
        AssertDefinition(
            Factory.Create<DeleteHedgeEndpoint>(default(object)!),
            "DELETE",
            "/api/v1/hedges/{hedgeId}",
            "BackOfficePolicy"
        );

    [Fact]
    public void GetByIdIsGETDeliveriesIdUnderReadPolicy() =>
        AssertDefinition(
            Factory.Create<GetDeliveryByIdEndpoint>(
                new FakeDeliveryRepository(),
                new FakeCacheService()
            ),
            "GET",
            "/api/v1/deliveries/{deliveryId}",
            "ReadPolicy"
        );

    [Fact]
    public void HistoryIsGETDeliveriesUnderReadPolicy() =>
        AssertDefinition(
            Factory.Create<GetDeliveryHistoryEndpoint>(new FakeDeliveryRepository()),
            "GET",
            "/api/v1/deliveries",
            "ReadPolicy"
        );

    [Fact]
    public void EventsCatchupIsGETEventsUnderReadPolicy() =>
        AssertDefinition(
            Factory.Create<GetEventsSinceEndpoint>(default(object)!),
            "GET",
            "/api/v1/events",
            "ReadPolicy"
        );

    [Fact]
    public void AnalyticsQueryIsPOSTAnalyticsQueryUnderReadPolicy() =>
        AssertDefinition(
            Factory.Create<AnalyticsQueryEndpoint>(default(object)!),
            "POST",
            "/api/v1/analytics/query",
            "ReadPolicy"
        );

    [Fact]
    public void ActivityIsGETActivityEntityIdUnderReadPolicy() =>
        AssertDefinition(
            Factory.Create<GetActivityEndpoint>(default(object)!),
            "GET",
            "/api/v1/activity/{entityName}/{entityId}",
            "ReadPolicy"
        );

    [Fact]
    public void InAppAgentStatusIsGETAgentStatusUnderReadPolicy() =>
        AssertDefinition(
            Factory.Create<GetInAppAgentStatusEndpoint>(default(object)!),
            "GET",
            "/api/v1/agent/status",
            "ReadPolicy"
        );

    private static IEnumerable<(
        BaseEndpoint Endpoint,
        string Verb,
        string Route,
        string Policy
    )> DomainEndpointDefinitions()
    {
        foreach (var definition in ContractDomainEndpointDefinitions())
        {
            yield return definition;
        }

        foreach (var definition in BioticketDomainEndpointDefinitions())
        {
            yield return definition;
        }

        foreach (var definition in CapacityBookingDomainEndpointDefinitions())
        {
            yield return definition;
        }

        foreach (var definition in TransferDomainEndpointDefinitions())
        {
            yield return definition;
        }

        foreach (var definition in GooCertificateDomainEndpointDefinitions())
        {
            yield return definition;
        }

        foreach (var definition in HedgeDomainEndpointDefinitions())
        {
            yield return definition;
        }

        foreach (var definition in MarketPriceDomainEndpointDefinitions())
        {
            yield return definition;
        }

        foreach (var definition in TaxTariffDomainEndpointDefinitions())
        {
            yield return definition;
        }

        foreach (var definition in DashboardDomainEndpointDefinitions())
        {
            yield return definition;
        }
    }

    private static IEnumerable<(
        BaseEndpoint Endpoint,
        string Verb,
        string Route,
        string Policy
    )> ContractDomainEndpointDefinitions()
    {
        yield return (
            Factory.Create<CreateContractEndpoint>(default(object)!),
            "POST",
            "/api/v1/contracts",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<GetContractByIdEndpoint>(default(object)!),
            "GET",
            "/api/v1/contracts/{contractId}",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<GetContractHistoryEndpoint>(default(object)!),
            "GET",
            "/api/v1/contracts",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<UpdateContractEndpoint>(default(object)!),
            "PUT",
            "/api/v1/contracts/{contractId}",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<DeactivateContractEndpoint>(default(object)!),
            "DELETE",
            "/api/v1/contracts/{contractId}",
            "BackOfficePolicy"
        );
    }

    private static IEnumerable<(
        BaseEndpoint Endpoint,
        string Verb,
        string Route,
        string Policy
    )> BioticketDomainEndpointDefinitions()
    {
        yield return (
            Factory.Create<CreateBioticketEndpoint>(default(object)!),
            "POST",
            "/api/v1/biotickets",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<GetBioticketByIdEndpoint>(default(object)!),
            "GET",
            "/api/v1/biotickets/{bioticketId}",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<GetBioticketHistoryEndpoint>(default(object)!),
            "GET",
            "/api/v1/biotickets",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<UpdateBioticketEndpoint>(default(object)!),
            "PUT",
            "/api/v1/biotickets/{bioticketId}",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<CancelBioticketEndpoint>(default(object)!),
            "DELETE",
            "/api/v1/biotickets/{bioticketId}",
            "BackOfficePolicy"
        );
    }

    private static IEnumerable<(
        BaseEndpoint Endpoint,
        string Verb,
        string Route,
        string Policy
    )> CapacityBookingDomainEndpointDefinitions()
    {
        yield return (
            Factory.Create<CreateCapacityBookingEndpoint>(default(object)!),
            "POST",
            "/api/v1/capacity-bookings",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<GetCapacityBookingByIdEndpoint>(default(object)!),
            "GET",
            "/api/v1/capacity-bookings/{capacityBookingId}",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<GetCapacityBookingHistoryEndpoint>(default(object)!),
            "GET",
            "/api/v1/capacity-bookings",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<UpdateCapacityBookingEndpoint>(default(object)!),
            "PUT",
            "/api/v1/capacity-bookings/{capacityBookingId}",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<DeleteCapacityBookingEndpoint>(default(object)!),
            "DELETE",
            "/api/v1/capacity-bookings/{capacityBookingId}",
            "BackOfficePolicy"
        );
    }

    private static IEnumerable<(
        BaseEndpoint Endpoint,
        string Verb,
        string Route,
        string Policy
    )> TransferDomainEndpointDefinitions()
    {
        yield return (
            Factory.Create<CreateTransferEndpoint>(default(object)!),
            "POST",
            "/api/v1/transfers",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<GetTransferByIdEndpoint>(default(object)!),
            "GET",
            "/api/v1/transfers/{transferId}",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<GetTransferHistoryEndpoint>(default(object)!),
            "GET",
            "/api/v1/transfers",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<UpdateTransferEndpoint>(default(object)!),
            "PUT",
            "/api/v1/transfers/{transferId}",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<CancelTransferEndpoint>(default(object)!),
            "DELETE",
            "/api/v1/transfers/{transferId}",
            "BackOfficePolicy"
        );
    }

    private static IEnumerable<(
        BaseEndpoint Endpoint,
        string Verb,
        string Route,
        string Policy
    )> GooCertificateDomainEndpointDefinitions()
    {
        yield return (
            Factory.Create<CreateGooCertificateEndpoint>(default(object)!),
            "POST",
            "/api/v1/goo-certificates",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<GetGooCertificateByIdEndpoint>(default(object)!),
            "GET",
            "/api/v1/goo-certificates/{gooCertificateTransactionId}",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<GetGooCertificateHistoryEndpoint>(default(object)!),
            "GET",
            "/api/v1/goo-certificates",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<UpdateGooCertificateEndpoint>(default(object)!),
            "PUT",
            "/api/v1/goo-certificates/{gooCertificateTransactionId}",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<RequestGooBatchExportEndpoint>(default(object)!),
            "POST",
            "/api/v1/goo-certificates/{gooCertificateTransactionId}/request-batch-export",
            "BackOfficePolicy"
        );
        yield return (
            Factory.Create<DeleteGooCertificateEndpoint>(default(object)!),
            "DELETE",
            "/api/v1/goo-certificates/{gooCertificateTransactionId}",
            "BackOfficePolicy"
        );
    }

    private static IEnumerable<(
        BaseEndpoint Endpoint,
        string Verb,
        string Route,
        string Policy
    )> HedgeDomainEndpointDefinitions()
    {
        yield return (
            Factory.Create<CreateHedgeEndpoint>(default(object)!),
            "POST",
            "/api/v1/hedges",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<GetHedgeByIdEndpoint>(default(object)!),
            "GET",
            "/api/v1/hedges/{hedgeId}",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<GetHedgeHistoryEndpoint>(default(object)!),
            "GET",
            "/api/v1/hedges",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<UpdateHedgeEndpoint>(default(object)!),
            "PUT",
            "/api/v1/hedges/{hedgeId}",
            "TraderPolicy"
        );
        yield return (
            Factory.Create<DeleteHedgeEndpoint>(default(object)!),
            "DELETE",
            "/api/v1/hedges/{hedgeId}",
            "BackOfficePolicy"
        );
    }

    private static IEnumerable<(
        BaseEndpoint Endpoint,
        string Verb,
        string Route,
        string Policy
    )> MarketPriceDomainEndpointDefinitions()
    {
        yield return (
            Factory.Create<UpsertMarketPriceEndpoint>(default(object)!),
            "PUT",
            "/api/v1/market-prices/{priceDate}",
            "AdminPolicy"
        );
        yield return (
            Factory.Create<GetMarketPriceByDateEndpoint>(default(object)!),
            "GET",
            "/api/v1/market-prices/{priceDate}",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<GetMarketPriceHistoryEndpoint>(default(object)!),
            "GET",
            "/api/v1/market-prices",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<DeleteMarketPriceEndpoint>(default(object)!),
            "DELETE",
            "/api/v1/market-prices/{priceDate}",
            "AdminPolicy"
        );
    }

    private static IEnumerable<(
        BaseEndpoint Endpoint,
        string Verb,
        string Route,
        string Policy
    )> TaxTariffDomainEndpointDefinitions()
    {
        yield return (
            Factory.Create<CreateTaxTariffEndpoint>(default(object)!),
            "POST",
            "/api/v1/tax-tariffs",
            "AdminPolicy"
        );
        yield return (
            Factory.Create<GetTaxTariffByIdEndpoint>(default(object)!),
            "GET",
            "/api/v1/tax-tariffs/{taxTariffId}",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<GetTaxTariffHistoryEndpoint>(default(object)!),
            "GET",
            "/api/v1/tax-tariffs",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<UpdateTaxTariffEndpoint>(default(object)!),
            "PUT",
            "/api/v1/tax-tariffs/{taxTariffId}",
            "AdminPolicy"
        );
        yield return (
            Factory.Create<DeleteTaxTariffEndpoint>(default(object)!),
            "DELETE",
            "/api/v1/tax-tariffs/{taxTariffId}",
            "AdminPolicy"
        );
    }

    private static IEnumerable<(
        BaseEndpoint Endpoint,
        string Verb,
        string Route,
        string Policy
    )> DashboardDomainEndpointDefinitions()
    {
        yield return (
            Factory.Create<GetDashboardEndpoint>(default(object)!),
            "GET",
            "/api/v1/dashboards/{dashboardId}",
            "ReadPolicy"
        );
        yield return (
            Factory.Create<SaveDashboardEndpoint>(
                default(object)!,
                default(object)!,
                default(object)!,
                default(object)!
            ),
            "PUT",
            "/api/v1/dashboards/{dashboardId}",
            "ReadPolicy"
        );
    }

    [Fact]
    public void AllDomainEndpointRoutesVerbsAndPoliciesArePinned() =>
        AssertDefinitions(DomainEndpointDefinitions());

    private static void AssertDefinitions(
        IEnumerable<(BaseEndpoint Endpoint, string Verb, string Route, string Policy)> definitions
    )
    {
        foreach (var (endpoint, verb, route, policy) in definitions)
        {
            AssertDefinition(endpoint, verb, route, policy);
        }
    }

    private static void AssertDefinition(
        BaseEndpoint endpoint,
        string verb,
        string route,
        string policy
    )
    {
        Assert.Equal([route], endpoint.Definition.Routes);
        Assert.Equal([verb], endpoint.Definition.Verbs);
        Assert.Contains(policy, PoliciesOf(endpoint.Definition), StringComparer.Ordinal);
        Assert.False(IsAnonymous(endpoint.Definition), "endpoint must not allow anonymous access");
    }

    private static List<string> PoliciesOf(EndpointDefinition definition)
    {
        var policies = new List<string>();
        foreach (
            var member in definition
                .GetType()
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        )
        {
            if (!member.Name.Contains("olic", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = member switch
            {
                PropertyInfo property when property.GetIndexParameters().Length == 0 =>
                    property.GetValue(definition),
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
        foreach (
            var member in definition
                .GetType()
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        )
        {
            if (!member.Name.Contains("nonymous", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = member switch
            {
                PropertyInfo property when property.GetIndexParameters().Length == 0 =>
                    property.GetValue(definition),
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
                dependencies
            );
    }
}
