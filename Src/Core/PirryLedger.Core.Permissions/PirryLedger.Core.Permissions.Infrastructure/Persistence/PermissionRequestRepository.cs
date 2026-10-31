using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Infrastructure.Persistence;

internal sealed class PermissionRequestRepository : IPermissionRequestRepository
{
    private readonly PermissionsDbContext _context;

    public PermissionRequestRepository(PermissionsDbContext context)
    {
        _context = context;
    }

    public async Task<PermissionRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _context.PermissionRequests
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionRequest>> ListByStateAsync(
        PermissionRequestState state,
        CancellationToken cancellationToken = default)
    {
        return await _context.PermissionRequests
            .Where(request => request.State == state)
            .OrderBy(request => request.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionRequest>> ListByRequesterAsync(
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PermissionRequests
            .Where(request => request.RequesterId == requesterId)
            .OrderByDescending(request => request.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task SaveAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        if (!_context.PermissionRequests.Local.Any(local => local.Id == request.Id))
        {
            _context.PermissionRequests.Add(request);
        }

        return _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsPendingAsync(
        Guid requesterId,
        string permissionName,
        CancellationToken cancellationToken = default) =>
        _context.PermissionRequests.AnyAsync(
            request => request.RequesterId == requesterId
                && request.RequestedPermission == permissionName
                && request.State == PermissionRequestState.Pending,
            cancellationToken);
}