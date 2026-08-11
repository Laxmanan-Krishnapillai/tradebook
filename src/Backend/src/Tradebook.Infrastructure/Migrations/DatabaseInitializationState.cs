namespace Tradebook.Infrastructure.Migrations;

public sealed class DatabaseInitializationState
{
    private Exception? _failure;
    private int _isReady;

    public bool IsReady => Volatile.Read(ref _isReady) == 1;

    public Exception? Failure => Volatile.Read(ref _failure);

    public void MarkReady()
    {
        Volatile.Write(ref _failure, null);
        Volatile.Write(ref _isReady, 1);
    }

    public void MarkFailed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Volatile.Write(ref _failure, exception);
        Volatile.Write(ref _isReady, 0);
    }
}
