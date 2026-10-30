using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Domain;

namespace PirryLedger.Core.Permissions.Infrastructure.Security;

// RF-GP-05 + ADR 008 + ADR 011: implementacion de IPermissionAuthority.
//
// IsKnown consulta el catalogo (unico punto donde se declaran los nombres
// validos, incluida la convencion CancelInvoice:<id>) y las otras dos
// operaciones delegan en el repositorio de concesiones: ExistsAsync para la
// consulta y ConsumeAsync para el borrado de una sola vez. La autoridad no
// toca el DbContext directamente.
public sealed class EfPermissionAuthority : IPermissionAuthority
{
    private readonly IPermissionGrantRepository _grants;

    public EfPermissionAuthority(IPermissionGrantRepository grants)
    {
        _grants = grants;
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
}
