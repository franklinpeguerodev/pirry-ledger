using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Application;

// Lo unico que Application necesita saber de como se guardan los usuarios. Lo
// implementa Infrastructure, donde vive el DbContext.
//
// Ningun caso de uso escribe en una tabla: pasa por aqui, para que la forma de
// guardar sea una decision de Infrastructure y no se disperse por el codigo.
public interface IUserRepository
{
    // Normaliza el correo antes de buscar, para que "Juan@Correo.com" y
    // "juan@correo.com" sean la misma persona y no dos registros.
    Task<Usuario?> BuscarPorCorreoAsync(string correo, CancellationToken cancellationToken = default);

    Task<Usuario?> BuscarPorIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task GuardarAsync(Usuario usuario, CancellationToken cancellationToken = default);

    // Lo necesita el seed del primer Administrador para ser idempotente: si ya
    // hay uno, no se crea otro. Es un metodo propio y no un filtro de
    // BuscarPorCorreoAsync porque la pregunta es "existe alguno", no "este".
    Task<bool> ExisteAlgunAdministradorAsync(CancellationToken cancellationToken = default);
}