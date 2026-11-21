using PirryLedger.Core.Notifications.Domain.Enums;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Application;

// Lo unico que Application necesita saber de la cola. Lo implementa
// Infrastructure, donde vive el DbContext.
//
// Listar no inicia intentos. ClaimAsync persiste atómicamente un único reclamo.
// Recuperar Processing solo se permite al procesador que posee el bloqueo.
public interface IEmailQueueRepository
{
    Task<IReadOnlyList<EmailQueueItem>> ListPendingAsync(int limit, CancellationToken cancellationToken = default);
    Task<EmailQueueItem?> ClaimAsync(Guid id, DateTime attemptedAtUtc, CancellationToken cancellationToken = default);
    Task<int> RecoverInterruptedAsync(DateTime changedAtUtc, CancellationToken cancellationToken = default);
    Task CancelPendingNotificationAsync(Guid id, DateTime changedAtUtc, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);
}
