using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.Notifications.Application;

public sealed class ManageNotificationPreference(INotificationDeliveryStore store, IBranchScopeAccessor scope,
    IApplicationTransaction transaction, IClock clock)
{
    public Task<bool> GetAsync(CancellationToken cancellationToken = default) =>
        store.EmailEnabledAsync(Administrator(), cancellationToken);

    public async Task SetAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var userId = Administrator();
        await transaction.ExecuteAsync(async () =>
        {
            await store.SetEmailPreferenceAsync(userId, enabled, clock.UtcNow, cancellationToken);
            return 0;
        }, cancellationToken);
    }

    private Guid Administrator() => scope.IsGlobal && scope.UserId is { } id && id != Guid.Empty
        ? id : throw new NotificationPreferenceDeniedException();
}

public sealed class NotificationPreferenceDeniedException : Exception
{
    public NotificationPreferenceDeniedException() : base("Solo un Administrador puede configurar su correo de notificaciones.") { }
}
