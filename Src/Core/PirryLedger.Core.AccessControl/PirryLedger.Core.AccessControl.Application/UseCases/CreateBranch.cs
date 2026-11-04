using PirryLedger.Core.AccessControl.Domain.Entities;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class CreateBranch
{
    private readonly IBranchRepository _branches;

    public CreateBranch(IBranchRepository branches)
    {
        _branches = branches;
    }

    public async Task<Branch> ExecuteAsync(string name, string address, CancellationToken cancellationToken = default)
    {
        var branch = Branch.Create(name, address);
        await _branches.SaveAsync(branch, cancellationToken);
        return branch;
    }
}
