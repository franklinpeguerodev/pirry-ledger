using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Infrastructure;

public sealed class NotificationInboxRepository(NotificationsDbContext context) : INotificationInboxRepository
{
    public async Task<IReadOnlyList<Notification>> ListOwnAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Notifications.AsNoTracking().Where(notification => notification.RecipientUserId == userId)
            .OrderByDescending(notification => notification.CreatedAtUtc).ThenBy(notification => notification.Id)
            .ToListAsync(cancellationToken);

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId, DateTime readAtUtc, CancellationToken cancellationToken)
    {
        var own = context.Notifications.Where(notification => notification.RecipientUserId == userId && notification.Id == notificationId);
        var changed = await own.Where(notification => notification.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(notification => notification.ReadAtUtc, readAtUtc), cancellationToken);
        return changed > 0 || await own.AnyAsync(cancellationToken);
    }

    public Task<int> MarkAllReadAsync(Guid userId, DateTime readAtUtc, CancellationToken cancellationToken) =>
        context.Notifications.Where(notification => notification.RecipientUserId == userId && notification.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(notification => notification.ReadAtUtc, readAtUtc), cancellationToken);
}
