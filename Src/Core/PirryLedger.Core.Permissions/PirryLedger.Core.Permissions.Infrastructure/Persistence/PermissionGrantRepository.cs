using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Domain.Entities;

namespace PirryLedger.Core.Permissions.Infrastructure.Persistence;

internal sealed class PermissionGrantRepository : IPermissionGrantRepository
{
    private readonly PermissionsDbContext _context;

    public PermissionGrantRepository(PermissionsDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(PermissionGrant grant, CancellationToken cancellationToken = default)
    {
        // Upsert por (user_id, permission_name). El indice unico
        // ux_per_permission_grants_user_permission (ADR 008) impide que el camino
        // de insercion funcione dos veces para el mismo par. Si la fila existe,
        // re-asignamos los sellos de tiempo y autor sobre la entidad existente
        // en vez de intentar un INSERT que la BD rechaza con 23505. Si no,
        // anadimos la nueva fila.
        var existing = await _context.PermissionGrants
            .FirstOrDefaultAsync(g =>
                g.UserId == grant.UserId &&
                g.PermissionName == grant.PermissionName,
                cancellationToken);

        if (existing is not null)
        {
            existing.Regrant(grant.GrantedAtUtc, grant.GrantedByUserId);
        }
        else
        {
            if (!_context.PermissionGrants.Local.Any(local => local.Id == grant.Id))
            {
                _context.PermissionGrants.Add(grant);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default) =>
        _context.PermissionGrants
            .AsNoTracking()
            .AnyAsync(grant =>
                grant.UserId == userId
                && grant.PermissionName == permissionName,
                cancellationToken);
}