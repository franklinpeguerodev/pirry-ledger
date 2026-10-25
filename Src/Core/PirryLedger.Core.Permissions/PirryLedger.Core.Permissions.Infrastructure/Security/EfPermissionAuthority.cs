using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Permissions.Domain;
using PirryLedger.Core.Permissions.Infrastructure.Persistence;

namespace PirryLedger.Core.Permissions.Infrastructure.Security;

// RF-GP-05 + ADR 008: implementacion de IPermissionAuthority.
//
// IsKnown consulta el catalogo (unico punto donde se declaran los nombres
// validos) y UserHasPermissionAsync hace un EXISTS sobre per_permission_grants.
// La fila la inserta ApprovePermissionRequest, en la misma transaccion que
// aprueba la solicitud, asi que el permiso concedido se ve en la siguiente
// peticion del solicitante.
public sealed class EfPermissionAuthority : IPermissionAuthority
{
    private readonly PermissionsDbContext _context;

    public EfPermissionAuthority(PermissionsDbContext context)
    {
        _context = context;
    }

    public bool IsKnown(string permissionName)
    {
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            return false;
        }

        return PermissionCatalog.KnownPermissions().Contains(permissionName);
    }

    public Task<bool> UserHasPermissionAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(permissionName))
        {
            return Task.FromResult(false);
        }

        return _context.PermissionGrants
            .AsNoTracking()
            .AnyAsync(grant =>
                grant.UserId == userId
                && grant.PermissionName == permissionName,
                cancellationToken);
    }
}