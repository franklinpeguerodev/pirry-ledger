using PirryLedger.Core.Contracts.Notifications;

namespace PirryLedger.Core.Notifications.Application;

public interface INotificationDeliveryStore
{
    Task<bool> EmailEnabledAsync(Guid userId, CancellationToken cancellationToken);
    Task DeliverAsync(string eventKey, NotificationRecipient recipient, string subject, string body,
        DateTime createdAtUtc, bool email, CancellationToken cancellationToken);
    Task SetEmailPreferenceAsync(Guid userId, bool enabled, DateTime changedAtUtc, CancellationToken cancellationToken);
}
