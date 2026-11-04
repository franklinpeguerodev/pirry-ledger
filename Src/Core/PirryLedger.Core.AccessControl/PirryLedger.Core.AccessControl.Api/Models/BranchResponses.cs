using PirryLedger.Core.AccessControl.Domain.Entities;

namespace PirryLedger.Core.AccessControl.Api;

public sealed record BranchResponse(Guid Id, string Name, string Address, bool IsAll, bool Active)
{
    public static BranchResponse From(Branch branch) =>
        new(branch.Id, branch.Name, branch.Address, branch.IsAll, branch.Active);
}
