using PirryLedger.Core.Notifications.Domain.Enums;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Application;

// Lo unico que Application necesita saber de la cola. Lo implementa
// Infrastructure, donde vive el DbContext.
//
// ClaimPendingAsync tiene que ser atomico: dos procesos que ejecuten el
// emisor a la vez no pueden recibir el mismo Email. Esa garantia no se puede
// expresar en la interfaz, asi que la implementacion es la unica que puede
// cumplirla y por eso el metodo se llama Reclamar y no Obtener.
public interface IEmailQueueRepository
{
    Task<IReadOnlyList<EmailQueueItem>> ClaimPendingAsync(int limite, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);
}
