using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Application;

public interface INotificationInboxRepository
{
    Task<IReadOnlyList<Notification>> ListOwnAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, DateTime readAtUtc, CancellationToken cancellationToken);
    Task<int> MarkAllReadAsync(Guid userId, DateTime readAtUtc, CancellationToken cancellationToken);
}
