using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class BranchDirectory : IBranchDirectory
{
    private readonly IBranchRepository _branches;

    public BranchDirectory(IBranchRepository branches)
    {
        _branches = branches;
    }

    public async Task<BranchScope?> FindAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.FindByIdAsync(branchId, cancellationToken);
        return branch is null
            ? null
            : new BranchScope(branch.Id, branch.Name, branch.Address, branch.IsAll);
    }
}
