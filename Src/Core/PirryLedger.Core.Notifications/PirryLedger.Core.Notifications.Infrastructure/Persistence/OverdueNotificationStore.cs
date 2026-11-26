using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Infrastructure;

public sealed class OverdueNotificationStore(NotificationsDbContext context) : IOverdueNotificationStore
{
    public Task<bool> IsRecordedAsync(string eventKey, CancellationToken cancellationToken = default) =>
        context.OverdueNotificationRecords.AsNoTracking().AnyAsync(record => record.EventKey == eventKey, cancellationToken);

    public async Task<bool> TryRecordAsync(string eventKey, DateTime recordedAtUtc, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("El registro de atraso requiere la transacción de su publicación.");
        var record = OverdueNotificationRecord.Create(eventKey, recordedAtUtc);
        var inserted = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO not_overdue_notification_records (id, event_key, recorded_at_utc)
            VALUES ({record.Id}, {record.EventKey}, {record.RecordedAtUtc})
            ON CONFLICT (event_key) DO NOTHING
            """, cancellationToken);
        return inserted == 1;
    }

    public Task<string?> FindNotificationEventKeyAsync(Guid notificationId, CancellationToken cancellationToken = default) =>
        context.Notifications.AsNoTracking().Where(notification => notification.Id == notificationId)
            .Select(notification => notification.EventKey).SingleOrDefaultAsync(cancellationToken);
}
