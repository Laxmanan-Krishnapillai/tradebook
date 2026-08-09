using System.Diagnostics.CodeAnalysis;
using Xunit;

// FastEndpoints' Factory.Create shares static test-service state; parallel test
// classes race inside AddTestServices. Endpoint unit tests must run serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Existing FastEndpoints test helpers expose optional cancellation tokens, but these
// synchronous in-memory unit tests have no asynchronous operation to cancel.
[assembly: SuppressMessage("Usage", "xUnit1051", Justification = "In-memory endpoint unit tests")]
