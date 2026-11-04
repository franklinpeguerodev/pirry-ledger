using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Domain;
using PirryLedger.Core.Permissions.Domain.Entities;

namespace PirryLedger.Core.Permissions.Infrastructure.Security;

// RF-GP-05 + ADR 008 + ADR 011 + ADR 014: implementacion de IPermissionAuthority.
//
// IsKnown consulta el catalogo (unico punto donde se declaran los nombres
// validos, incluida la convencion CancelInvoice:<id>) y las otras tres
// operaciones delegan en el repositorio de concesiones: ExistsAsync para la
// consulta, ConsumeAsync para el borrado de una sola vez y SaveAsync para la
// concesion directa de GrantAsync. La autoridad no toca el DbContext
// directamente.
public sealed class EfPermissionAuthority : IPermissionAuthority
{
    private readonly IPermissionGrantRepository _grants;
    private readonly IClock _clock;

    public EfPermissionAuthority(IPermissionGrantRepository grants, IClock clock)
    {
        _grants = grants;
        _clock = clock;
    }

    public bool IsKnown(string permissionName) =>
        PermissionCatalog.IsKnown(permissionName);

    public Task<bool> UserHasPermissionAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(permissionName))
        {
            return Task.FromResult(false);
        }

        return _grants.ExistsAsync(userId, permissionName, cancellationToken);
    }

    public Task<bool> ConsumeAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(permissionName))
        {
            return Task.FromResult(false);
        }

        return _grants.ConsumeAsync(userId, permissionName, cancellationToken);
    }

    // ADR 014: concesion directa. El llamante ya es la aprobacion (es un
    // Administrator autenticado), asi que no hay solicitud que pasar por la
    // maquina de estados. El repositorio hace upsert por (usuario, permiso).
    public Task GrantAsync(
        Guid userId,
        string permissionName,
        Guid grantedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(permissionName))
        {
            return Task.CompletedTask;
        }

        var grant = PermissionGrant.Create(
            userId,
            permissionName,
            _clock.UtcNow,
            grantedByUserId);

        return _grants.SaveAsync(grant, cancellationToken);
    }
}
