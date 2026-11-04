using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class BranchScopeAccessor : IBranchScopeAccessor
{
    public Guid? BranchId { get; set; }

    public bool IsGlobal { get; set; }
}
