namespace PirryLedger.Core.Contracts.Auth;

public interface IBranchScopeAccessor
{
    Guid? BranchId { get; set; }

    Guid? UserId { get; set; }

    bool IsGlobal { get; set; }
}
