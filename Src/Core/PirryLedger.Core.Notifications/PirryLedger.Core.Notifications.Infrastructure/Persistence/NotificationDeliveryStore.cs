using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;

namespace PirryLedger.Core.Notifications.Infrastructure;

public sealed class NotificationDeliveryStore(NotificationsDbContext context, IClock clock) : INotificationDeliveryStore
{
    public async Task<bool> EmailEnabledAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null)
            await LockRecipientAsync(userId, cancellationToken);
        return await context.NotificationPreferences.AsNoTracking().Where(preference => preference.UserId == userId)
            .Select(preference => (bool?)preference.EmailEnabled).SingleOrDefaultAsync(cancellationToken) ?? NotificationPreference.DefaultEmailEnabled;
    }

    public async Task DeliverAsync(string eventKey, NotificationRecipient recipient, string subject, string body,
        DateTime createdAtUtc, bool email, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("La notificación debe participar en la transacción de su operación.");
        var notification = Notification.Create(recipient.UserId, subject, body, createdAtUtc, eventKey);
        var id = notification.Id;
        var inserted = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO not_notifications (id, recipient_user_id, subject, body, created_at_utc, event_key)
            VALUES ({id}, {recipient.UserId}, {notification.Subject}, {notification.Body}, {createdAtUtc}, {eventKey})
            ON CONFLICT (event_key, recipient_user_id) DO NOTHING
            """, cancellationToken);
        if (inserted == 1 && email)
        {
            context.EmailQueueItems.Add(EmailQueueItem.ForNotification(id, recipient.UserId,
                recipient.Email, notification.Subject, System.Net.WebUtility.HtmlEncode(notification.Body), createdAtUtc));
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SetEmailPreferenceAsync(Guid userId, bool enabled, DateTime changedAtUtc, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("La preferencia requiere una transacción.");
        await LockRecipientAsync(userId, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO not_notification_preferences (user_id, email_enabled, updated_at_utc)
            VALUES ({userId}, {enabled}, {changedAtUtc})
            ON CONFLICT (user_id) DO UPDATE SET email_enabled = EXCLUDED.email_enabled,
                updated_at_utc = EXCLUDED.updated_at_utc
            """, cancellationToken);
        if (!enabled)
            await context.EmailQueueItems.Where(email => email.NotificationRecipientUserId == userId
                && email.NotificationId != null && email.Status == EmailStatus.Pending)
                .ExecuteUpdateAsync(update => update.SetProperty(email => email.Status, EmailStatus.Cancelled)
                    .SetProperty(email => email.StatusChangedAtUtc, changedAtUtc), cancellationToken);
    }

    private async Task LockRecipientAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var defaultEnabled = NotificationPreference.DefaultEmailEnabled;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO not_notification_preferences (user_id, email_enabled, updated_at_utc)
            VALUES ({userId}, {defaultEnabled}, {now}) ON CONFLICT (user_id) DO NOTHING
            """, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT user_id FROM not_notification_preferences WHERE user_id = {userId} FOR UPDATE
            """, cancellationToken);
    }
}
