using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.Ports;

public interface IDebtRepository
{
    Task<IReadOnlyList<Invoice>> ListAsync(Guid? branchId, CancellationToken cancellationToken);

    Task<Invoice?> FindAsync(Guid invoiceId, Guid? branchId, CancellationToken cancellationToken);

    Task<T> InInvoiceTransactionAsync<T>(Guid invoiceId, Guid? branchId,
        Func<Invoice, Task<T>> action, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}
