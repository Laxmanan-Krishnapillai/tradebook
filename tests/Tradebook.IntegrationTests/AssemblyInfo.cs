using System.Diagnostics.CodeAnalysis;
using Xunit;

// FastEndpoints keeps a process-wide service resolver. Disposing one concurrently running
// WebApplicationFactory can therefore invalidate another test host's resolver mid-request.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Cancellation behavior is covered by the API itself; these hermetic fixture calls must
// complete their database cleanup even when the surrounding test context is cancelled.
[assembly: SuppressMessage("Usage", "xUnit1051", Justification = "Hermetic fixture cleanup")]
