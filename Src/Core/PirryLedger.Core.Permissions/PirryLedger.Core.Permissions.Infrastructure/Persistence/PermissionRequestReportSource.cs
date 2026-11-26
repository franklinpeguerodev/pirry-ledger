using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Reports;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Infrastructure.Persistence;

public sealed class PermissionRequestReportSource(PermissionsDbContext context) : IPermissionRequestReportSource
{
    public async Task<IReadOnlyList<PermissionRequestCount>> AggregateAsync(DateTime fromUtc,
        DateTime untilUtc, IReadOnlyCollection<Guid> branchIds, CancellationToken cancellationToken = default)
    {
        var rows = await context.PermissionRequests.AsNoTracking()
            .Where(request => request.CreatedAtUtc >= fromUtc && request.CreatedAtUtc < untilUtc
                && branchIds.Contains(request.BranchId))
            .GroupBy(request => new { request.BranchId, request.State })
            .Select(group => new { group.Key.BranchId, group.Key.State, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new PermissionRequestCount(row.BranchId, row.State switch
        {
            PermissionRequestState.Pending => PermissionReportState.Pending,
            PermissionRequestState.Approved => PermissionReportState.Approved,
            PermissionRequestState.Applied => PermissionReportState.Applied,
            PermissionRequestState.Rejected => PermissionReportState.Rejected,
            _ => throw new InvalidOperationException("El estado de solicitud es inválido.")
        }, row.Count)).ToArray();
    }
}
