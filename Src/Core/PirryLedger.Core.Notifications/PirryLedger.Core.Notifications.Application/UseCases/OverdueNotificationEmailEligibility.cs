using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.Notifications.Application;

public sealed class OverdueNotificationEmailEligibility(IOverdueNotificationStore store,
    IEnumerable<IOverdueNotificationSource> sources, IClock clock) : INotificationEmailEligibility
{
    public async Task<bool> IsRelevantAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var key = await store.FindNotificationEventKeyAsync(notificationId, cancellationToken);
        if (key is null || !(key.StartsWith("TransferOverdue:", StringComparison.Ordinal)
            || key.StartsWith("BusinessDayOverdue:", StringComparison.Ordinal))) return true;
        var parts = key.Split(':');
        if (parts.Length != 2 || !Enum.TryParse<NotificationEventKind>(parts[0], out var kind)
            || !Guid.TryParse(parts[1], out var sourceId))
            throw new InvalidOperationException("El aviso de atraso no tiene un origen válido.");
        var source = sources.SingleOrDefault(candidate => candidate.Handles(kind))
            ?? throw new InvalidOperationException("No se puede comprobar la vigencia del aviso de atraso.");
        return await source.IsRelevantAsync(kind, sourceId, clock.UtcNow, cancellationToken);
    }
}
