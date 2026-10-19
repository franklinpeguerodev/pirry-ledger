using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Application;

// Lo unico que Application necesita saber de como se guardan los usuarios. Lo
// implementa Infrastructure, donde vive el DbContext.
//
// Ningun caso de uso escribe en una tabla: pasa por aqui, para que la forma de
// guardar sea una decision de Infrastructure y no se disperse por el codigo.
public interface IUserRepository
{
    // Normaliza el Email antes de buscar, para que "Juan@Email.com" y
    // "juan@Email.com" sean la misma persona y no dos registros.
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    // RF-CA-21: listar usuarios con su role y su estado.
    //
    // Trae TODOS, sin paginar. Con 2 o 3 empleados por sucursal una pagina de
    // diez seria mas complicacion que usuarios: el paginado se anade el dia que
    // haga falta, no antes.
    //
    // Ordena por Email para que la lista sea estable entre llamadas. Sin esto,
    // PostgreSQL no garantiza orden y el listado cambiaria entre peticiones sin
    // que nadie haya tocado nada.
    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(User user, CancellationToken cancellationToken = default);

    // Lo necesitan RF-CA-20 y el seed para contar Administrators activos.
    // Existe como metodo y no como filtro porque la pregunta es "cuantos hay",
    // no "este User es uno de ellos".
    Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default);

    // Lo necesita el seed del primer Administrator para ser idempotente: si ya
    // hay uno, no se crea otro. Es un metodo propio y no un filtro de
    // FindByEmailAsync porque la pregunta es "existe alguno", no "este".
    Task<bool> AnyAdministratorExistsAsync(CancellationToken cancellationToken = default);
}
