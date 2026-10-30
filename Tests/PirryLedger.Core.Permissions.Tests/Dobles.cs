using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Tests;

// RD-12: las pruebas del modulo viven sin base de datos ni SMTP. Todo lo
// que tocan las pruebas se sustituye por un doble. Los doubles aqui son
// locales al modulo de Permissions (no dependen de la Suite de AccessControl).

// Reloj falso. Lo que hace que las pruebas de fechas (RF-GP-03, RF-GP-05)
// sean deterministas: Advance(TimeSpan) en lugar de esperar.
internal sealed class FakeClock : IClock
{
    public FakeClock(DateTime now)
    {
        UtcNow = now;
    }

    public DateTime UtcNow { get; set; }

    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}

// Catalogo + autoridad de permisos en uno: las pruebas declaran que un
// permiso es conocido y luego deciden por UserId si esta concedido.
internal sealed class FakePermissionAuthority : IPermissionAuthority
{
    private readonly HashSet<string> _known;
    private readonly Dictionary<(Guid, string), bool> _granted = new();

    public FakePermissionAuthority(IEnumerable<string> known)
    {
        _known = new HashSet<string>(known, StringComparer.Ordinal);
    }

    public FakePermissionAuthority(params string[] known) : this((IEnumerable<string>)known)
    {
    }

    public bool IsKnown(string permissionName) =>
        !string.IsNullOrWhiteSpace(permissionName)
        && (_known.Contains(permissionName) || PermissionNames.IsCancelInvoice(permissionName));

    public Task<bool> UserHasPermissionAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(IsKnown(permissionName) && userId != Guid.Empty
            && _granted.TryGetValue((userId, permissionName), out var granted)
            && granted);

    public void Grant(Guid userId, string permissionName)
    {
        _granted[(userId, permissionName)] = true;
    }

    public void Revoke(Guid userId, string permissionName)
    {
        _granted[(userId, permissionName)] = false;
    }

    // RF-NEG-06: mismo contrato que la autoridad real. Una concesion se
    // consume una sola vez; la segunda llamada devuelve false.
    public Task<bool> ConsumeAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        var key = (userId, permissionName);

        if (userId == Guid.Empty
            || !IsKnown(permissionName)
            || !_granted.TryGetValue(key, out var granted)
            || !granted)
        {
            return Task.FromResult(false);
        }

        _granted.Remove(key);
        return Task.FromResult(true);
    }
}

// Repositorio en memoria para PermissionRequest. La entidad es mutable: las
// pruebas inspeccionan los campos despues de aprobar/rechazar.
internal sealed class FakePermissionRequestRepository : IPermissionRequestRepository
{
    private readonly Dictionary<Guid, PermissionRequest> _requests = new();

    public int SavedCount { get; private set; }

    public PermissionRequest Create(
        Guid requesterId,
        string requestedPermission,
        DateTime utcNow,
        PermissionRequestState initialState = PermissionRequestState.Pending)
    {
        var request = PermissionRequest.Create(requesterId, requestedPermission, utcNow);
        _requests[request.Id] = request;
        return request;
    }

    public Task<PermissionRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_requests.TryGetValue(id, out var r) ? r : null);

    public Task<IReadOnlyList<PermissionRequest>> ListByStateAsync(
        PermissionRequestState state,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PermissionRequest>>(
            _requests.Values.Where(r => r.State == state).ToList());

    public Task<IReadOnlyList<PermissionRequest>> ListByRequesterAsync(
        Guid requesterId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PermissionRequest>>(
            _requests.Values.Where(r => r.RequesterId == requesterId).ToList());

    public Task SaveAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        _requests[request.Id] = request;
        SavedCount++;
        return Task.CompletedTask;
    }
}

// Repositorio en memoria para PermissionGrant. Basta con un EXISTS y un
// SaveAsync para las pruebas de la pieza.
internal sealed class FakePermissionGrantRepository : IPermissionGrantRepository
{
    private readonly Dictionary<(Guid, string), PermissionGrant> _grants = new();

    public int SavedCount { get; private set; }

    public Task SaveAsync(PermissionGrant grant, CancellationToken cancellationToken = default)
    {
        _grants[(grant.UserId, grant.PermissionName)] = grant;
        SavedCount++;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_grants.ContainsKey((userId, permissionName)));

    // RF-NEG-06: el doble borra la entrada; una segunda llamada devuelve
    // false, igual que el repositorio real.
    public Task<bool> ConsumeAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_grants.Remove((userId, permissionName)));
}