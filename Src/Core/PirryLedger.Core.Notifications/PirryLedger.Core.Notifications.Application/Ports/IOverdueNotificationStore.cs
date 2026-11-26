namespace PirryLedger.Core.Notifications.Application;

public interface IOverdueNotificationStore
{
    Task<bool> IsRecordedAsync(string eventKey, CancellationToken cancellationToken = default);
    Task<bool> TryRecordAsync(string eventKey, DateTime recordedAtUtc, CancellationToken cancellationToken = default);
    Task<string?> FindNotificationEventKeyAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
