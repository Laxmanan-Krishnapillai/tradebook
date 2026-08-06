using System.Collections;
using System.Reflection;
using FastEndpoints;
using Tradebook.Api.Features.Analytics;
using Tradebook.Api.Features.Auth.Login;
using Tradebook.Api.Features.Events;
using Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;
using Tradebook.Api.Features.PhysicalDeliveries.DeletePhysicalDelivery;
using Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryById;
using Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryHistory;
using Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery;

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
}
