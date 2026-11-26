namespace PirryLedger.Core.Contracts.Notifications;

public enum NotificationEventKind
{
    PermissionRequested,
    PermissionApproved,
    PermissionRejected,
    TransferRegistered,
    TransferConfirmed,
    TransferRejected,
    DebtCreated,
    DebtCashPayment,
    DebtTransferConfirmed,
    DebtTransferRejected,
    BusinessDayClosed,
    TransferOverdue,
    BusinessDayOverdue
}

// SourceId identifica la ocurrencia, no el intento de publicación.
public sealed record NotificationEvent(NotificationEventKind Kind, Guid SourceId,
    string Detail, Guid? RecipientUserId = null, Guid? BranchId = null)
{
    public string Key => $"{Kind}:{SourceId:D}";
}

public interface INotificationPublisher
{
    Task PublishAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken = default);
}

public sealed record NotificationRecipient(Guid UserId, string Email, bool IsAdministrator, Guid? BranchId, string Name = "");

public interface INotificationRecipientDirectory
{
    Task<NotificationRecipient?> FindActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationRecipient>> ListActiveAdministratorsAsync(CancellationToken cancellationToken = default);
}
