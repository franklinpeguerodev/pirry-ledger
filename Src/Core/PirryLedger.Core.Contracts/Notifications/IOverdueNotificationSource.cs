namespace PirryLedger.Core.Contracts.Notifications;

public sealed record OverdueNotificationCandidate(NotificationEventKind Kind, Guid SourceId, DateTime DueAtUtc)
{
    public string Key => $"{Kind}:{SourceId:D}";
}

public interface IOverdueNotificationSource
{
    bool Handles(NotificationEventKind kind);
    Task<IReadOnlyList<OverdueNotificationCandidate>> FindCandidatesAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
    // Se invoca dentro de la transacción común; el origen conserva su bloqueo.
    Task<NotificationEvent?> RevalidateAsync(OverdueNotificationCandidate candidate, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<bool> IsRelevantAsync(NotificationEventKind kind, Guid sourceId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
