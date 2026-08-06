using Xunit;

// FastEndpoints' Factory.Create shares static test-service state; parallel test
// classes race inside AddTestServices. Endpoint unit tests must run serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
