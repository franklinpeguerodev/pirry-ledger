using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Infrastructure.Persistence;

public sealed class PendingInvoiceCancellations(PermissionsDbContext context) : IPendingInvoiceCancellations
{
    public async Task<IReadOnlySet<Guid>> FindAsync(IReadOnlyCollection<Guid> invoiceIds, CancellationToken ct)
    {
        if (invoiceIds.Count == 0) return new HashSet<Guid>();
        var names = invoiceIds.Select(PermissionNames.CancelInvoice).ToArray();
        var pending = await context.PermissionRequests
            .Where(x => x.State == PermissionRequestState.Pending && names.Contains(x.RequestedPermission))
            .Select(x => x.RequestedPermission).ToListAsync(ct);
        return pending.Select(x => { PermissionNames.IsCancelInvoice(x, out var id); return id; }).ToHashSet();
    }
}
