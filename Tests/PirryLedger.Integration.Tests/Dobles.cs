using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Integration.Tests;

// RD-12: la prueba corre sin base de datos, sin SMTP y sin levantar la
// aplicacion. El reloj es el unico doble que necesita el negocio, para que las
// fechas no dependan del dia en que se ejecute la suite.
internal sealed class FakeClock : IClock
{
    public FakeClock(DateTime now)
    {
        UtcNow = now;
    }

    public DateTime UtcNow { get; set; }

    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}

// Decision 2, opcion A. Las dos piezas que se integran son Business y
// Permissions, y las dos son reales. AccessControl solo aporta la sesion, y sus
// dos puertos de persistencia se sustituyen por estas clases en memoria, igual
// que hace la suite de Permissions: Login y Authenticate, que son quienes
// deciden si una sesion vale y que role tiene, siguen siendo los reales.
internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users = new();

    public User Create(
        string name,
        string email,
        string hash,
        DateTime utcNow,
        bool active,
        Role role = Role.Standard)
    {
        var user = User.Create(name, email, hash, utcNow);
        if (active)
        {
            user.Activate();
        }

        if (role != user.Role)
        {
            user.ChangeRole(role);
        }

        _users.Add(user);
        return user;
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Email == email.Trim().ToLowerInvariant()));

    public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == userId));

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(_users.OrderBy(u => u.Email).ToList());

    public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Count(u => u.Role == Role.Administrator && u.Active));

    public Task SaveAsync(User user, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> AnyAdministratorExistsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Any(u => u.Role == Role.Administrator));
}

// ISessionRepository minimo: lo que Login y Authenticate necesitan para crear,
// validar y cerrar sesiones. Es una copia del que usa la suite de Permissions,
// porque compartirlo exigiria un proyecto Shared (decision abierta).
internal sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly List<Session> _sessions = new();

    public Task SaveAsync(Session session, CancellationToken cancellationToken = default)
    {
        _sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<Session?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sessions.FirstOrDefault(s => s.TokenHash == tokenHash));

    public Task<bool> CloseByHashAsync(string tokenHash, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var session = _sessions.FirstOrDefault(candidate => candidate.TokenHash == tokenHash);

        if (session is null || session.IsClosed)
        {
            return Task.FromResult(false);
        }

        session.Close(utcNow);
        return Task.FromResult(true);
    }
}
