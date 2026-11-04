using PirryLedger.Core.AccessControl.Domain.Entities;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class UpdateBranch
{
    private readonly IBranchRepository _branches;
    private readonly IUserRepository _users;

    public UpdateBranch(IBranchRepository branches, IUserRepository users)
    {
        _branches = branches;
        _users = users;
    }

    public async Task<Branch> ExecuteAsync(
        Guid branchId,
        string name,
        string address,
        bool active,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.FindByIdAsync(branchId, cancellationToken)
            ?? throw new AdministrationOperationRejectedException("No existe esa sucursal.");

        if (await _branches.FindByNameAsync(name, branch.Id, cancellationToken) is not null)
        {
            throw new AdministrationOperationRejectedException(
                "Ya existe una sucursal con ese nombre.");
        }

        if (branch.IsAll && (!active || name.Trim() != branch.Name || address.Trim() != branch.Address))
        {
            throw new AdministrationOperationRejectedException(
                "La sucursal global no se puede modificar.");
        }

        if (branch.Active && !active && await _users.CountActiveByBranchAsync(branch.Id, cancellationToken) > 0)
        {
            throw new AdministrationOperationRejectedException(
                "Para desactivar una sucursal, todos sus usuarios deben estar desactivados.");
        }

        branch.Update(name, address);
        branch.SetActive(active);
        await _branches.SaveAsync(branch, cancellationToken);
        return branch;
    }
}
