using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class AssignUserBranch
{
    private readonly IUserRepository _users;
    private readonly IBranchRepository _branches;
    private readonly Authenticate _authenticate;

    public AssignUserBranch(
        IUserRepository users,
        IBranchRepository branches,
        Authenticate authenticate)
    {
        _users = users;
        _branches = branches;
        _authenticate = authenticate;
    }

    public async Task ExecuteAsync(
        string plainToken,
        Guid userId,
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        await _authenticate.ExecuteWithRoleAsync(
            plainToken,
            RoleRequirements.RequiredRole(Operation.AssignUserBranch),
            cancellationToken);

        var branch = await _branches.FindByIdAsync(branchId, cancellationToken)
            ?? throw new AdministrationOperationRejectedException("No existe esa sucursal.");

        if (branch.IsAll || !branch.Active)
        {
            throw new AdministrationOperationRejectedException(
                "El usuario Standard debe tener una sucursal física activa.");
        }

        var user = await _users.FindByIdAsync(userId, cancellationToken)
            ?? throw new AdministrationOperationRejectedException("No existe ese usuario.");

        if (user.Role != Role.Standard)
        {
            throw new AdministrationOperationRejectedException(
                "Solo se puede asignar sucursal a un usuario Standard.");
        }

        user.AssignBranch(branch.Id);
        await _users.SaveAsync(user, cancellationToken);
    }
}
