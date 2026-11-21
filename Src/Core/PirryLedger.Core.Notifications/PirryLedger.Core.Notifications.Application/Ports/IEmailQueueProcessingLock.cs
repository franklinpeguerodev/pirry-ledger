namespace PirryLedger.Core.Notifications.Application;

public interface IEmailQueueProcessingLock
{
    Task<IEmailQueueProcessingLease?> TryAcquireAsync(CancellationToken cancellationToken = default);
}

public interface IEmailQueueProcessingLease : IAsyncDisposable
{
    Task VerifyAsync(CancellationToken cancellationToken = default);
}
