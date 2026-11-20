using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.Ports;

public interface IBusinessDayRepository
{
    Task<T> InBranchTransactionAsync<T>(Guid branchId, Func<Task<T>> action, CancellationToken cancellationToken);
    Task<BusinessDay?> LatestAsync(Guid branchId, CancellationToken cancellationToken);
    Task<BusinessDay?> FindAsync(Guid id, Guid branchId, CancellationToken cancellationToken);
    Task<bool> HasLaterAsync(BusinessDay day, CancellationToken cancellationToken);
    Task AddAsync(BusinessDay day, CancellationToken cancellationToken);
    Task<IReadOnlyList<Invoice>> InvoicesAsync(Guid businessDayId, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
