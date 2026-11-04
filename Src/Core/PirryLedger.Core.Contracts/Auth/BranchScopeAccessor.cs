namespace PirryLedger.Core.Contracts.Auth;

public interface IBranchScopeAccessor
{
    Guid? BranchId { get; set; }

    bool IsGlobal { get; set; }
}
