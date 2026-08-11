using System.Data.Common;
using Tradebook.Infrastructure.Migrations;

namespace Tradebook.UnitTests;

public sealed class MigrationFailureClassifierTests
{
    [Fact]
    public void TransientDatabaseFailureIsRetryable()
    {
        Assert.True(MigrationFailureClassifier.IsTransient(new TestDbException(true)));
    }

    [Fact]
    public void WrappedTransientDatabaseFailureIsRetryable()
    {
        var exception = new InvalidOperationException(
            "Database migration failed.",
            new TestDbException(true)
        );

        Assert.True(MigrationFailureClassifier.IsTransient(exception));
    }

    [Fact]
    public void TimeoutIsRetryable()
    {
        Assert.True(MigrationFailureClassifier.IsTransient(new TimeoutException()));
    }

    [Fact]
    public void PermanentDatabaseFailureIsNotRetryable()
    {
        var exception = new InvalidOperationException(
            "Database migration failed.",
            new TestDbException(false)
        );

        Assert.False(MigrationFailureClassifier.IsTransient(exception));
    }

    [Fact]
    public void UnrelatedApplicationFailureIsNotRetryable()
    {
        Assert.False(MigrationFailureClassifier.IsTransient(new InvalidOperationException()));
    }

    private sealed class TestDbException(bool isTransient) : DbException
    {
        public override bool IsTransient { get; } = isTransient;
    }
}
