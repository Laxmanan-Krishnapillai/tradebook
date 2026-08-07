using System.Text.Json.Serialization;
using Tradebook.Core.DTOs;
using Tradebook.Core.Analytics;
using Tradebook.Api.Features.Analytics;

namespace Tradebook.Api;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CreatePhysicalDeliveryRequest))]
[JsonSerializable(typeof(CreatePhysicalDeliveryResponse))]
[JsonSerializable(typeof(PhysicalDeliveryDetailsDto))]
[JsonSerializable(typeof(GetDeliveryHistoryRequest))]
[JsonSerializable(typeof(GetDeliveryHistoryResponse))]
[JsonSerializable(typeof(UpdatePhysicalDeliveryRequest))]
[JsonSerializable(typeof(DeletePhysicalDeliveryRequest))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(Features.Events.GetEventsSinceResponse))]
[JsonSerializable(typeof(JsonQueryAst))]
[JsonSerializable(typeof(AnalyticsQueryResponse))]
[JsonSerializable(typeof(SaveDashboardRequest))]
[JsonSerializable(typeof(SaveDashboardResponse))]
[JsonSerializable(typeof(FastEndpoints.ErrorResponse))]
public partial class AppJsonSerializerContext : JsonSerializerContext;
