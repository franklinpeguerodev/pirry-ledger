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
        if (!_context.PermissionGrants.Local.Any(local => local.Id == grant.Id))
        {
            _context.PermissionGrants.Add(grant);
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