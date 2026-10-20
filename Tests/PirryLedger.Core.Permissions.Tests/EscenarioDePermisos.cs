using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;
using PirryLedger.Core.AccessControl.Application;

namespace PirryLedger.Core.Permissions.Tests;

// Scenario compartido por las pruebas de la pieza de Permissions.
//
// Construye un Administrator (seed via las variables FIRST_ADMIN_*) y un
// Standard adicional, ambos con sesiones abiertas, y los expone como tokens
// y como entidades UserId. Las pruebas llaman a los casos de uso pasando
// estos tokens, sin tocar HTTP.
internal sealed class EscenarioDePermisos
{
    private const string ValidPassword = "abc12345";
    private const string AdminEmailValue = "admin@ejemplo.com";
    private const string StandardEmailValue = "empleado@ejemplo.com";

    public EscenarioDePermisos(DateTime? now = null)
    {
        var start = now ?? new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        Clock = new FakeClock(start);

        // Argon2id real con los mismos parametros que la aplicacion.
        var hasher = new Argon2idPasswordHasher(new Argon2Parameters(
            MemoryKb: 19456,
            Iterations: 2,
            Parallelism: 1));

        // Users en memoria: el Administrator nace ya activo (mismo criterio
        // que la semilla real). El Standard tambien.
        Users = new InMemoryUserRepository();
        Admin = Users.Create("Admin", AdminEmailValue, hasher.Hash(ValidPassword), start, active: true, role: Role.Administrator);
        Standard = Users.Create("Empleado", StandardEmailValue, hasher.Hash(ValidPassword), start, active: true, role: Role.Standard);

        Sessions = new InMemorySessionRepository();

        // Authenticate real: las pruebas llaman a los casos de uso, que a
        // su vez llaman a Authenticate.ExecuteAsync/ExecuteWithRoleAsync.
        // El Authority falso se inyecta con un solo permiso conocido.
        Authority = new FakePermissionAuthority("ViewSalesSummary");
        Authenticate = new Authenticate(Sessions, Users, Clock, Authority);

        // Login real para emitir tokens en claro para los dos usuarios.
        Login = new Login(Users, Sessions, hasher, Clock, TimeSpan.FromHours(8));
    }

    public FakeClock Clock { get; }

    public InMemoryUserRepository Users { get; }

    public InMemorySessionRepository Sessions { get; }

    public FakePermissionAuthority Authority { get; }

    public Authenticate Authenticate { get; }

    public Login Login { get; }

    public User Admin { get; }

    public User Standard { get; }

    public string AdminEmail => AdminEmailValue;

    public string StandardEmail => StandardEmailValue;

    public string AdminPassword => ValidPassword;

    public string StandardPassword => ValidPassword;

    // Inicia sesion como Administrator y devuelve el token en claro.
    public Task<string> OpenAdminSessionAsync() =>
        Login.ExecuteAsync(AdminEmailValue, ValidPassword);

    // Inicia sesion como Standard y devuelve el token en claro.
    public Task<string> OpenStandardSessionAsync() =>
        Login.ExecuteAsync(StandardEmailValue, ValidPassword);
}

// IUserRepository minimo: lo que las pruebas necesitan para que
// Authenticate valide la sesion (buscar por Id, contar administradores).
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

    public Task<List<User>> FindAllAdministratorsAsync() =>
        Task.FromResult(_users.Where(u => u.Role == Role.Administrator).ToList());
}

// ISessionRepository minimo: lo que Login y Authenticate necesitan para
// crear y validar sesiones.
internal sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly List<Session> _sessions = new();

    public int SavedCount => _sessions.Count;

    public Task SaveAsync(Session session, CancellationToken cancellationToken = default)
    {
        _sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<Session?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sessions.FirstOrDefault(s => s.TokenHash == tokenHash));

    public Task<bool> CloseByHashAsync(string tokenHash, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var s = _sessions.FirstOrDefault(x => x.TokenHash == tokenHash);
        if (s is null || s.IsClosed)
        {
            return Task.FromResult(false);
        }
        s.Close(utcNow);
        return Task.FromResult(true);
    }
}