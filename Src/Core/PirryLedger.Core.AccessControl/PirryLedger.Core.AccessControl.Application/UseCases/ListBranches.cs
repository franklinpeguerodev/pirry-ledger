namespace PirryLedger.Core.AccessControl.Application;

public sealed class ListBranches
{
    private readonly IBranchRepository _branches;

    public ListBranches(IBranchRepository branches)
    {
        _branches = branches;
    }

    public Task<IReadOnlyList<Domain.Entities.Branch>> ExecuteAsync(
        CancellationToken cancellationToken = default) =>
        _branches.ListAsync(cancellationToken);
}
