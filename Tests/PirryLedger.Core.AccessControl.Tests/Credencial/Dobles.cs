using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Permissions;
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

// RF-GP-05 y ADR 014: doble de IPermissionAuthority para las pruebas de
// AccessControl que no son del modulo de Permissions. Por defecto nadie tiene
// permisos; los nombres conocidos se declaran en el constructor y las
// concesiones se llevan en memoria.
//
// Desde ADR 014 este doble tiene que recordar concesiones de verdad: el cambio
// de contrasena y la recuperacion las consumen, y una prueba que no pueda
// concederlas no podria distinguir "sin aprobacion" de "aprobacion usada".
internal sealed class FakePermissionAuthority : IPermissionAuthority
{
    private readonly HashSet<string> _known;
    private readonly HashSet<(Guid UserId, string Permission)> _granted = [];

    public FakePermissionAuthority(params string[] known)
    {
        _known = new HashSet<string>(known, StringComparer.Ordinal);
    }

    public bool IsKnown(string permissionName) =>
        !string.IsNullOrWhiteSpace(permissionName) && _known.Contains(permissionName);

    public Task<bool> UserHasPermissionAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_granted.Contains((userId, permissionName)));

    // RF-NEG-06: una concesion se consume una sola vez.
    public Task<bool> ConsumeAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_granted.Remove((userId, permissionName)));

    // ADR 014: concesion directa, la que hace ForcePasswordReset. El doble no
    // valida el catalogo aqui a proposito: igual que la implementacion real,
    // GrantAsync no decide si el nombre es valido, solo registra la concesion.
    public Task GrantAsync(
        Guid userId,
        string permissionName,
        Guid grantedByUserId,
        CancellationToken cancellationToken = default)
    {
        _granted.Add((userId, permissionName));

        return Task.CompletedTask;
    }

    // Solo para pruebas: simula una aprobacion previa sin pasar por el caso de
    // uso de Permissions.
    public void Grant(Guid userId, string permissionName) =>
        _granted.Add((userId, permissionName));
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

// RF-CA-15 y RF-CA-16: el alta y el reenvio guardan un token de un solo uso.
// Este doble lo deja en memoria; la pieza no necesita saber como se persiste.
internal sealed class FakeActivationTokenRepository : IActivationTokenRepository
{
    private readonly List<ActivationToken> _tokens = [];

    public IReadOnlyList<ActivationToken> Saved => _tokens;

    public Task<ActivationToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_tokens.FirstOrDefault(token => token.TokenHash == tokenHash));

    public Task InvalidatePendingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _tokens.RemoveAll(token => token.UserId == userId && token.UsedAtUtc is null);

        return Task.CompletedTask;
    }

    public Task SaveAsync(ActivationToken token, CancellationToken cancellationToken = default)
    {
        _tokens.RemoveAll(existing => existing.Id == token.Id);
        _tokens.Add(token);

        return Task.CompletedTask;
    }
}

// RF-NOT-08: la cola es el unico camino de salida del correo. Este doble guarda
// lo que se encolaria, para poder afirmar que el enlace de activacion se encolo
// y que el codigo en claro solo viaja dentro del cuerpo.
internal sealed class InMemoryEmailQueue : IEmailQueue
{
    public List<QueuedEmail> Emails { get; } = [];

    public Task EnqueueAsync(QueuedEmail email, CancellationToken cancellationToken = default)
    {
        Emails.Add(email);

        return Task.CompletedTask;
    }
}

// RF-CA-09 y ADR 014: los codigos de recuperacion se guardan en memoria. El
// doble conserva tambien los usados, para poder afirmar que un codigo no se
// reutiliza.
internal sealed class InMemoryRecoveryCodeRepository : IRecoveryCodeRepository
{
    private readonly List<RecoveryCode> _codes = [];

    public IReadOnlyList<RecoveryCode> Saved => _codes;

    public Task<RecoveryCode?> FindByHashAsync(string codeHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_codes.FirstOrDefault(code => code.CodeHash == codeHash));

    public Task InvalidatePendingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _codes.RemoveAll(code => code.UserId == userId && !code.IsUsed);

        return Task.CompletedTask;
    }

    public Task SaveAsync(RecoveryCode code, CancellationToken cancellationToken = default)
    {
        _codes.RemoveAll(existing => existing.Id == code.Id);
        _codes.Add(code);

        return Task.CompletedTask;
    }
}

// Hash determinista y barato. Las pruebas de contrasena comprueban reglas de
// negocio, no el algoritmo: el Argon2id real se ejercita en las pruebas de
// Session y del hasher.
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hash:{password}";

    public bool Verify(string password, string storedHash) => storedHash == Hash(password);

    public string CreateDummyHash() => "dummy";
}

// ADR 012 y ADR 014: la solicitud de permiso en nombre del usuario. El doble
// deduplica las pendientes igual que el gateway real, para que la prueba del 409
// del cambio de contrasena pueda reproducir el segundo intento.
internal sealed class FakePermissionRequestGateway : IPermissionRequestGateway
{
    private readonly HashSet<(Guid RequesterId, string Permission)> _pending = [];

    public int CreatedCount { get; private set; }

    public IReadOnlyCollection<(Guid RequesterId, string Permission)> Pending => _pending;

    public Task<PermissionRequestOutcome> RequestAsync(
        Guid requesterId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        if (!_pending.Add((requesterId, permissionName)))
        {
            return Task.FromResult(PermissionRequestOutcome.AlreadyPending);
        }

        CreatedCount++;

        return Task.FromResult(PermissionRequestOutcome.Created);
    }
}
