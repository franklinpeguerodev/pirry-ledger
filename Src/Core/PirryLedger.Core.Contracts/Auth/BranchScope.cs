namespace PirryLedger.Core.Contracts.Auth;

public sealed record BranchScope(Guid Id, string Name, string Address, bool IsGlobal);

public interface IBranchDirectory
{
    Task<BranchScope?> FindAsync(Guid branchId, CancellationToken cancellationToken = default);
}
