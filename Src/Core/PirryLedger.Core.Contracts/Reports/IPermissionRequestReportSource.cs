namespace PirryLedger.Core.Contracts.Reports;

public enum PermissionReportState { Pending, Approved, Applied, Rejected }
public sealed record PermissionRequestCount(Guid BranchId, PermissionReportState State, int Count);

public interface IPermissionRequestReportSource
{
    Task<IReadOnlyList<PermissionRequestCount>> AggregateAsync(DateTime fromUtc,
        DateTime untilUtc, IReadOnlyCollection<Guid> branchIds, CancellationToken cancellationToken = default);
}
