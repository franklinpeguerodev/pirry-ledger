using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Reports;
using PirryLedger.Core.Reports.Domain;

namespace PirryLedger.Core.Reports.Application;

public sealed record PermissionStateTotals(int Pending, int Approved, int Applied, int Rejected)
{
    public int Total => Pending + Approved + Applied + Rejected;
}
public sealed record BranchPermissionTotals(Guid BranchId, string BranchName, bool IsGlobal, PermissionStateTotals Counts);
public sealed record PermissionRequestReport(DateOnly From, DateOnly To,
    IReadOnlyList<BranchPermissionTotals> Branches, PermissionStateTotals Counts);
public sealed class ReportAccessDeniedException() : Exception("Solo un Administrador puede consultar reportes.");

public sealed class GetPermissionRequestReport(IPermissionRequestReportSource source,
    IBranchDirectory directory, IBranchScopeAccessor scope)
{
    public async Task<PermissionRequestReport> ExecuteAsync(DateOnly from, DateOnly to,
        IReadOnlyCollection<Guid> branchIds, CancellationToken cancellationToken = default)
    {
        if (!scope.IsGlobal || scope.UserId is null || scope.UserId == Guid.Empty)
            throw new ReportAccessDeniedException();
        var period = new ReportPeriod(from, to).ToUtc();
        var available = await directory.ListAsync(cancellationToken);
        var selected = branchIds.Count == 0 ? available : available.Where(branch => branchIds.Contains(branch.Id)).ToArray();
        if (branchIds.Any(id => id == Guid.Empty || !available.Any(branch => branch.Id == id)))
            throw new ArgumentException("La selección contiene sucursales inválidas.");
        if (branchIds.Any(id => available.Any(branch => branch.Id == id && branch.IsGlobal)))
            selected = available;
        var ids = selected.Select(branch => branch.Id).Distinct().ToArray();
        var counts = await source.AggregateAsync(period.FromUtc, period.UntilUtc, ids, cancellationToken);
        var rows = selected.OrderBy(branch => branch.Name).Select(branch => new BranchPermissionTotals(
            branch.Id, branch.Name, branch.IsGlobal, Sum(counts.Where(row => row.BranchId == branch.Id)))).ToArray();
        return new(from, to, rows, Sum(counts));
    }

    private static PermissionStateTotals Sum(IEnumerable<PermissionRequestCount> rows)
    {
        var counts = rows.ToArray();
        int Count(PermissionReportState state) => counts.Where(row => row.State == state).Sum(row => row.Count);
        return new(Count(PermissionReportState.Pending), Count(PermissionReportState.Approved),
            Count(PermissionReportState.Applied), Count(PermissionReportState.Rejected));
    }
}
