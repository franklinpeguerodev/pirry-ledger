using PirryLedger.Core.AccessControl.Domain.Entities;

namespace PirryLedger.Core.AccessControl.Application;

public interface IBranchRepository
{
    Task<Branch?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Branch>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(Branch branch, CancellationToken cancellationToken = default);
}
