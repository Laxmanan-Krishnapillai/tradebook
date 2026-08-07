using Xunit;

// FastEndpoints keeps a process-wide service resolver. Disposing one concurrently running
// WebApplicationFactory can therefore invalidate another test host's resolver mid-request.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
