using FastEndpoints;
using Tradebook.Api.Features.GooCertificates;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class GooCertificateEndpointHandlerTests
{
    private static readonly Guid ActorId = Guid.Parse("9d4c73f8-f16b-4e6b-a5e5-1d811346b96b");

    private static CreateGooCertificateTransactionRequest CreateRequest() => new(
        "a07TG00000PMLtSYAX",
        "7265-17552",
        "Dena-Internal transaction",
        "847513",
        "NL",
        Guid.NewGuid(),
        "Producer AS",
        2.75m,
        new DateOnly(2026, 1, 1),
        Guid.NewGuid(),
        "Customer GmbH",
        "Dena",
        "Processing",
        new DateOnly(2026, 1, 2),
        100m,
        100m,
        "Biogas",
        "Export allocation");

    private static UpdateGooCertificateTransactionRequest UpdateRequest(Guid id) => new(
        id,
        "Dena-Internal transaction",
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Dena",
        "Processing",
        new DateOnly(2026, 1, 2),
        95m,
        95m,
        "Adjusted export allocation",
        3);

    [Fact]
    public async Task Create_returns_201_and_forwards_exact_request_actor_and_token()
    {
        var repository = new FakeGooCertificateEndpointRepository
        {
            CreateResult = DomainEndpointTestData.GooCertificate(version: 1)
        };
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<CreateGooCertificateEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);
        var request = CreateRequest();

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(201, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.CreateResult, endpoint.Response);
        var call = Assert.Single(repository.CreateCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(cancellation.Token, call.Token);
    }

    [Fact]
    public async Task Get_by_id_returns_200_and_forwards_exact_id()
    {
        var id = Guid.NewGuid();
        var repository = new FakeGooCertificateEndpointRepository
        {
            GetByIdResult = DomainEndpointTestData.GooCertificate(id)
        };
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<GetGooCertificateByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetGooCertificateByIdRequest(id), cancellation.Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.GetByIdResult, endpoint.Response);
        var call = Assert.Single(repository.GetByIdCalls);
        Assert.Equal(id, call.Id);
        Assert.Equal(cancellation.Token, call.Token);
    }

    [Fact]
    public async Task Get_by_id_returns_404_when_repository_has_no_match()
    {
        var id = Guid.NewGuid();
        var repository = new FakeGooCertificateEndpointRepository { GetByIdResult = null };
        var endpoint = Factory.Create<GetGooCertificateByIdEndpoint>(repository);

        await endpoint.HandleAsync(new GetGooCertificateByIdRequest(id), default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task History_returns_200_and_forwards_the_exact_filter_object()
    {
        var repository = new FakeGooCertificateEndpointRepository
        {
            HistoryResult = new([DomainEndpointTestData.GooCertificate()], 1, 2, 25, false)
        };
        var request = new GetGooCertificateHistoryRequest(
            Guid.NewGuid(), "Processing", new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 1), 2, 25);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<GetGooCertificateHistoryEndpoint>(repository);

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.HistoryResult, endpoint.Response);
        var call = Assert.Single(repository.HistoryCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(cancellation.Token, call.Token);
    }

    [Fact]
    public async Task Update_success_returns_200_and_does_not_fetch_current_state()
    {
        var id = Guid.NewGuid();
        var repository = new FakeGooCertificateEndpointRepository
        {
            UpdateResult = DomainEndpointTestData.GooCertificate(id, version: 4)
        };
        var request = UpdateRequest(id);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<UpdateGooCertificateEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.UpdateResult, endpoint.Response);
        var call = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(cancellation.Token, call.Token);
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Update_returns_404_when_update_and_current_lookup_are_missing()
    {
        var id = Guid.NewGuid();
        var repository = new FakeGooCertificateEndpointRepository
        {
            UpdateResult = null,
            GetByIdResult = null
        };
        var request = UpdateRequest(id);
        var endpoint = Factory.Create<UpdateGooCertificateEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        var updateCall = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, updateCall.Request);
        Assert.Equal(ActorId, updateCall.ActorId);
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task Update_returns_409_with_current_state_on_version_conflict()
    {
        var id = Guid.NewGuid();
        var current = DomainEndpointTestData.GooCertificate(id, version: 8);
        var repository = new FakeGooCertificateEndpointRepository
        {
            UpdateResult = null,
            GetByIdResult = current
        };
        var request = UpdateRequest(id);
        var endpoint = Factory.Create<UpdateGooCertificateEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        var updateCall = Assert.Single(repository.UpdateCalls);
        Assert.Same(request, updateCall.Request);
        Assert.Equal(ActorId, updateCall.ActorId);
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task Request_batch_export_success_returns_200_and_forwards_all_arguments()
    {
        var id = Guid.NewGuid();
        var repository = new FakeGooCertificateEndpointRepository
        {
            BatchExportResult = DomainEndpointTestData.GooCertificate(id, version: 4, status: "Batch export requested")
        };
        var request = new RequestGooBatchExportRequest(id, 3);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<RequestGooBatchExportEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(200, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(repository.BatchExportResult, endpoint.Response);
        var call = Assert.Single(repository.BatchExportCalls);
        Assert.Equal(id, call.Id);
        Assert.Equal(3, call.Version);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(cancellation.Token, call.Token);
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Request_batch_export_returns_404_when_transaction_is_missing()
    {
        var id = Guid.NewGuid();
        var repository = new FakeGooCertificateEndpointRepository
        {
            BatchExportResult = null,
            GetByIdResult = null
        };
        var request = new RequestGooBatchExportRequest(id, 3);
        var endpoint = Factory.Create<RequestGooBatchExportEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        var call = Assert.Single(repository.BatchExportCalls);
        Assert.Equal((id, 3L, ActorId), (call.Id, call.Version, call.ActorId));
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task Request_batch_export_returns_409_with_current_state_on_version_conflict()
    {
        var id = Guid.NewGuid();
        var current = DomainEndpointTestData.GooCertificate(id, version: 7);
        var repository = new FakeGooCertificateEndpointRepository
        {
            BatchExportResult = null,
            GetByIdResult = current
        };
        var request = new RequestGooBatchExportRequest(id, 3);
        var endpoint = Factory.Create<RequestGooBatchExportEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        var call = Assert.Single(repository.BatchExportCalls);
        Assert.Equal((id, 3L, ActorId), (call.Id, call.Version, call.ActorId));
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }

    [Fact]
    public async Task Delete_success_returns_204_and_forwards_all_mutation_arguments()
    {
        var id = Guid.NewGuid();
        var repository = new FakeGooCertificateEndpointRepository { DeleteResult = null };
        var request = new DeleteGooCertificateTransactionRequest(id, "Duplicate certificate", 3);
        using var cancellation = new CancellationTokenSource();
        var endpoint = Factory.Create<DeleteGooCertificateEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, cancellation.Token);

        Assert.Equal(204, endpoint.HttpContext.Response.StatusCode);
        var call = Assert.Single(repository.DeleteCalls);
        Assert.Equal(id, call.Id);
        Assert.Equal(3, call.Version);
        Assert.Equal("Duplicate certificate", call.Reason);
        Assert.Equal(ActorId, call.ActorId);
        Assert.Equal(cancellation.Token, call.Token);
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Delete_returns_404_for_not_found_without_fetching_current_state()
    {
        var id = Guid.NewGuid();
        var repository = new FakeGooCertificateEndpointRepository { DeleteResult = MutationOutcome.NotFound };
        var request = new DeleteGooCertificateTransactionRequest(id, "Duplicate certificate", 3);
        var endpoint = Factory.Create<DeleteGooCertificateEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(404, endpoint.HttpContext.Response.StatusCode);
        var call = Assert.Single(repository.DeleteCalls);
        Assert.Equal((id, 3L, "Duplicate certificate", ActorId),
            (call.Id, call.Version, call.Reason, call.ActorId));
        Assert.Empty(repository.GetByIdCalls);
    }

    [Fact]
    public async Task Delete_returns_409_with_current_state_for_version_conflict()
    {
        var id = Guid.NewGuid();
        var current = DomainEndpointTestData.GooCertificate(id, version: 9);
        var repository = new FakeGooCertificateEndpointRepository
        {
            DeleteResult = MutationOutcome.VersionConflict,
            GetByIdResult = current
        };
        var request = new DeleteGooCertificateTransactionRequest(id, "Duplicate certificate", 3);
        var endpoint = Factory.Create<DeleteGooCertificateEndpoint>(
            context => context.User = DomainEndpointTestData.Principal(ActorId), repository);

        await endpoint.HandleAsync(request, default);

        Assert.Equal(409, endpoint.HttpContext.Response.StatusCode);
        Assert.Same(current, endpoint.Response);
        var call = Assert.Single(repository.DeleteCalls);
        Assert.Equal((id, 3L, "Duplicate certificate", ActorId),
            (call.Id, call.Version, call.Reason, call.ActorId));
        Assert.Equal(id, Assert.Single(repository.GetByIdCalls).Id);
    }
}
