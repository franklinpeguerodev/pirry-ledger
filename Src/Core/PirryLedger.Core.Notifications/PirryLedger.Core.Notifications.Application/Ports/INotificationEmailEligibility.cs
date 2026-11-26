namespace PirryLedger.Core.Notifications.Application;

public interface INotificationEmailEligibility
{
    Task<bool> IsRelevantAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
