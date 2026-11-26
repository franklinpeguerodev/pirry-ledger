using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.Notifications.Application;

public sealed class ManageNotificationInbox(INotificationInboxRepository repository,
    IBranchScopeAccessor scope, IClock clock)
{
    public async Task<IReadOnlyList<NotificationView>> ListAsync(CancellationToken cancellationToken = default) =>
        (await repository.ListOwnAsync(UserId(), cancellationToken)).Select(notification => new NotificationView(
            notification.Id, notification.Subject, notification.Body, notification.CreatedAtUtc, notification.ReadAtUtc)).ToArray();

    public async Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        if (!await repository.MarkReadAsync(UserId(), notificationId, clock.UtcNow, cancellationToken))
            throw new NotificationNotFoundException();
    }

    public Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default) =>
        repository.MarkAllReadAsync(UserId(), clock.UtcNow, cancellationToken);

    private Guid UserId() => scope.UserId is { } id && id != Guid.Empty
        ? id : throw new NotificationSessionRequiredException();
}

public sealed record NotificationView(Guid Id, string Subject, string Body, DateTime CreatedAtUtc, DateTime? ReadAtUtc);
public sealed class NotificationNotFoundException : Exception
{
    public NotificationNotFoundException() : base("La notificación indicada no existe.") { }
}
public sealed class NotificationSessionRequiredException : Exception
{
    public NotificationSessionRequiredException() : base("Se requiere una sesión válida.") { }
}
