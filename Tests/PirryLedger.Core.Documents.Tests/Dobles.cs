using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Documents.Application.Ports;
using PirryLedger.Core.Documents.Domain.Entities;

namespace PirryLedger.Core.Documents.Tests;

// RD-12: la pieza se prueba sin aplicacion, sin base de datos y sin red. Todo
// lo que tocan las pruebas se sustituye por un doble local a este proyecto.

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTime now)
    {
        UtcNow = now;
    }

    public DateTime UtcNow { get; set; }

    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}

// IUserRepository minimo: lo que Authenticate y Login necesitan.
internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users = new();

    public User Create(
        string name,
        string email,
        string hash,
        DateTime utcNow,
        bool active,
        Role role = Role.Standard,
        Guid? branchId = null)
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

        // El Administrator se queda en la sucursal global "Todas"; el Standard
        // necesita una sucursal fisica.
        if (branchId is not null)
        {
            user.AssignBranch(branchId.Value);
        }

        _users.Add(user);
        return user;
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Email == email.Trim().ToLowerInvariant()));

    public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == userId));

    public Task<int> CountActiveByBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Count(user => user.BranchId == branchId && user.Active));

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

// ISessionRepository minimo: crear y validar sesiones.
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
        var session = _sessions.FirstOrDefault(s => s.TokenHash == tokenHash);

        if (session is null || session.IsClosed)
        {
            return Task.FromResult(false);
        }

        session.Close(utcNow);
        return Task.FromResult(true);
    }
}

// Authenticate exige una autoridad de permisos; esta pieza no usa permisos,
// asi que la autoridad dice que no hay ninguno concedido.
internal sealed class FakePermissionAuthority : IPermissionAuthority
{
    public bool IsKnown(string permissionName) => false;

    public Task<bool> UserHasPermissionAsync(Guid userId, string permissionName, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> ConsumeAsync(Guid userId, string permissionName, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task GrantAsync(Guid userId, string permissionName, Guid grantedByUserId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

// Repositorio en memoria del registro. A proposito NO filtra los eliminados:
// asi la prueba comprueba que quien aplica RF-DOC-02 es el caso de uso y no la
// casualidad de que un repositorio concreto se acuerde de filtrar.
internal sealed class FakeDocumentRepository : IDocumentRepository
{
    private readonly Dictionary<Guid, Document> _documents = new();

    public int SaveCount { get; private set; }

    public Task SaveAsync(Document document, CancellationToken cancellationToken = default)
    {
        _documents[document.Id] = document;
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task<Document?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.TryGetValue(id, out var document) ? document : null);

    public Task<IReadOnlyList<Document>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Document>>(
            _documents.Values
                .Where(document => document.UploadedByUserId == ownerId)
                .OrderByDescending(document => document.UploadedAtUtc)
                .ToList());

    public Task<IReadOnlyList<Document>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Document>>(
            _documents.Values
                .OrderByDescending(document => document.UploadedAtUtc)
                .ToList());

    public Document Seed(Document document)
    {
        _documents[document.Id] = document;
        return document;
    }
}

internal enum StoreMode
{
    Ok,
    Unavailable,
    NotConfigured,
}

// Almacenamiento en memoria. Registra cuantas escrituras hubo, para poder
// afirmar que un rechazo no toco el almacenamiento.
internal sealed class FakeDocumentStore : IDocumentStore
{
    private readonly Dictionary<string, byte[]> _objects = new();

    public StoreMode Mode { get; set; } = StoreMode.Ok;

    public int PutCount { get; private set; }

    public IReadOnlyDictionary<string, byte[]> Objects => _objects;

    public Task PutAsync(
        string key,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (Mode == StoreMode.NotConfigured)
        {
            throw new PirryLedger.Core.Documents.Application.Exceptions.DocumentStorageNotConfiguredException();
        }

        if (Mode == StoreMode.Unavailable)
        {
            throw new PirryLedger.Core.Documents.Application.Exceptions.DocumentStorageUnavailableException();
        }

        PutCount++;
        _objects[key] = content;
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (Mode == StoreMode.NotConfigured)
        {
            throw new PirryLedger.Core.Documents.Application.Exceptions.DocumentStorageNotConfiguredException();
        }

        if (Mode == StoreMode.Unavailable)
        {
            throw new PirryLedger.Core.Documents.Application.Exceptions.DocumentStorageUnavailableException();
        }

        return Task.FromResult(_objects.TryGetValue(key, out var content) ? content : null);
    }
}
