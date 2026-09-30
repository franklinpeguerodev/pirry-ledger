using PirryLedger.Core.Notifications.Domain;

namespace PirryLedger.Core.Notifications.Application;

// Lo unico que Application necesita saber de la cola. Lo implementa
// Infrastructure, donde vive el DbContext.
//
// ReclamarPendientesAsync tiene que ser atomico: dos procesos que ejecuten el
// emisor a la vez no pueden recibir el mismo correo. Esa garantia no se puede
// expresar en la interfaz, asi que la implementacion es la unica que puede
// cumplirla y por eso el metodo se llama Reclamar y no Obtener.
public interface ICorreoEnColaRepository
{
    Task<IReadOnlyList<CorreoEnCola>> ReclamarPendientesAsync(int limite, CancellationToken cancellationToken = default);

    Task GuardarAsync(CancellationToken cancellationToken = default);

    Task<int> ContarPendientesAsync(CancellationToken cancellationToken = default);
}
