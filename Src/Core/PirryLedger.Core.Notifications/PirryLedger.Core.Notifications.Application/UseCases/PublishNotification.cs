using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.Notifications.Application;

public sealed class PublishNotification(INotificationRecipientDirectory directory, INotificationDeliveryStore store,
    IApplicationTransaction transaction, IClock clock) : INotificationPublisher
{
    public async Task PublishAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notificationEvent);
        if (notificationEvent.SourceId == Guid.Empty || string.IsNullOrWhiteSpace(notificationEvent.Detail))
            throw new ArgumentException("El evento requiere identidad y contenido válidos.", nameof(notificationEvent));
        var policy = NotificationDeliveryPolicy.For(notificationEvent.Kind);
        await transaction.ExecuteAsync(async () =>
        {
            IReadOnlyList<NotificationRecipient> recipients;
            if (policy.Administrators)
                recipients = await directory.ListActiveAdministratorsAsync(cancellationToken);
            else
            {
                if (notificationEvent.RecipientUserId is not { } id || id == Guid.Empty)
                    throw new ArgumentException("El evento requiere su destinatario.", nameof(notificationEvent));
                var recipient = await directory.FindActiveAsync(id, cancellationToken);
                recipients = recipient is null ? [] : [recipient];
            }

            foreach (var recipient in recipients.OrderBy(recipient => recipient.UserId))
            {
                var email = policy.Email && recipient.IsAdministrator
                    && await store.EmailEnabledAsync(recipient.UserId, cancellationToken);
                await store.DeliverAsync(notificationEvent.Key, recipient, policy.Subject,
                    notificationEvent.Detail, clock.UtcNow, email, cancellationToken);
            }
            return 0;
        }, cancellationToken);
    }
}

public sealed record NotificationDeliveryPolicy(bool Administrators, bool Email, string Subject)
{
    public static NotificationDeliveryPolicy For(NotificationEventKind kind) => kind switch
    {
        NotificationEventKind.PermissionRequested => new(true, true, "Solicitud de permiso pendiente"),
        NotificationEventKind.PermissionApproved => new(false, false, "Solicitud de permiso aprobada"),
        NotificationEventKind.PermissionRejected => new(false, false, "Solicitud de permiso rechazada"),
        NotificationEventKind.TransferRegistered => new(true, false, "Transferencia pendiente de verificación"),
        NotificationEventKind.TransferConfirmed => new(false, false, "Transferencia confirmada"),
        NotificationEventKind.TransferRejected => new(false, false, "Transferencia rechazada"),
        NotificationEventKind.DebtCreated => new(true, false, "Nueva factura fiada"),
        NotificationEventKind.DebtCashPayment => new(true, false, "Abono de deuda en efectivo"),
        NotificationEventKind.DebtTransferConfirmed => new(false, false, "Abono por transferencia confirmado"),
        NotificationEventKind.DebtTransferRejected => new(false, false, "Abono por transferencia rechazado"),
        NotificationEventKind.BusinessDayClosed => new(true, false, "Cierre de jornada"),
        NotificationEventKind.TransferOverdue => new(true, true, "Transferencia pendiente por más de cuatro días"),
        NotificationEventKind.BusinessDayOverdue => new(true, true, "Jornada pendiente de cierre"),
        _ => throw new ArgumentException("El tipo de evento de notificación no es válido.", nameof(kind))
    };
}
