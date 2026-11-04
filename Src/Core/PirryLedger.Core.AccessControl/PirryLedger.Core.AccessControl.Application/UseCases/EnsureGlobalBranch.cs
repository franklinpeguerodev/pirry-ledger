using PirryLedger.Core.AccessControl.Domain.Entities;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class EnsureGlobalBranch
{
    private readonly IBranchRepository _branches;

    public EnsureGlobalBranch(IBranchRepository branches)
    {
        _branches = branches;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (await _branches.FindByIdAsync(Branch.AllId, cancellationToken) is null)
        {
            await _branches.SaveAsync(Branch.CreateAll(), cancellationToken);
        }
    }
}
