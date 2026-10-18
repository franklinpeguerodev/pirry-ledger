using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RD-12: las piezas se prueban sin levantar la aplicacion ni tocar PostgreSQL.
// Los dobles de los repositorios viven aqui, en el proyecto de pruebas.
//
// El Clock falso es lo que hace que RF-CA-19 sea comprobable: los quince minutos
// de bloqueo se recorren instanciando otro Clock quince minutos adelante, sin
// esperar. Con el Clock real, esta prueba tardaria un cuarto de hora.

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTime now)
    {
        UtcNow = now;
    }

    public DateTime UtcNow { get; set; }

    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public User Create(string name, string email, string hash, DateTime utcNow, bool active, Role role = Role.Standard)
    {
        // Se crea y luego se activa o no desde fuera, porque el constructor de
        // User lo deja siempre inactivo (RF-CA-15).
        var user = User.Create(name, email, hash, utcNow);

        if (active)
        {
            user.Activate();
        }

        // Create no recibe role: el user nace con el Role por defecto y se cambia
        // despues con ChangeRole, que es el camino real.
        if (role != user.Role)
        {
            user.ChangeRole(role);
        }

        _users.Add(user);

        return user;
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users
            .FirstOrDefault(user => user.Email == email.Trim().ToLowerInvariant()));

    public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(user => user.Id == userId));

    public Task SaveAsync(User user, CancellationToken cancellationToken = default)
    {
        // El doble guarda la misma instancia, asi que las mutaciones de la entidad
        // ya estan visibles. No hace falta persistir nada.
        //
        // Aun asi hay que registrar la fila si no estaba: hay casos de uso (como
        // el seed del primer Administrator) que crean la entidad y la guardan
        // sin pasar antes por Create, y sin esto no se podrian volver a buscar.
        if (!_users.Any(registered => registered.Id == user.Id))
        {
            _users.Add(user);
        }

        return Task.CompletedTask;
    }

    // RF-CA-21. Ordena por Email como el repositorio real, para que las pruebas
    // no dependan del orden en que se crean los Users.
    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(_users
            .OrderBy(user => user.Email)
            .ToList());

    // RF-CA-20: solo los activos, como el repositorio real.
    public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_users
            .Count(user => user.Role == Role.Administrator && user.Active));

    public Task<bool> AnyAdministratorExistsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Any(user => user.Role == Role.Administrator));

    // Solo para pruebas: el repositorio real no necesita listar Administradores.
    // Sirve para comprobar que el seed no creo dos.
    public Task<List<User>> FindAllAdministratorsAsync() =>
        Task.FromResult(_users.Where(user => user.Role == Role.Administrator).ToList());
}

internal sealed class FakeSessionRepository : ISessionRepository
{
    private readonly List<Session> _sessions = [];

    public int SavedCount => _sessions.Count;

    public Session? Find(string tokenHash) =>
        _sessions.FirstOrDefault(session => session.TokenHash == tokenHash);

    public Task SaveAsync(Session session, CancellationToken cancellationToken = default)
    {
        _sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<Session?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(Find(tokenHash));

    public Task<bool> CloseByHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var session = Find(tokenHash);

        if (session is null || session.IsClosed)
        {
            return Task.FromResult(false);
        }

        session.Close(utcNow);

        return Task.FromResult(true);
    }
}
