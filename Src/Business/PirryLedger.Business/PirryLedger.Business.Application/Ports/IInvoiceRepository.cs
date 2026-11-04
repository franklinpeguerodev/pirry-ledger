using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.Ports;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, Guid? branchId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Invoice>> ListAsync(Guid? branchId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
