using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Application.Ports;

public sealed record OverdueTransfer(Guid Id, Guid InvoiceId, Guid BranchId, string BranchName, decimal Amount,
    PaymentMethod Method, PaymentStatus Status, InvoiceState InvoiceState, DateTime RegisteredAtUtc);
public sealed record OverdueBusinessDay(BusinessDay Day, string BranchName);

public interface IOverdueBillingRepository
{
    Task<IReadOnlyList<OverdueTransfer>> FindPendingTransfersAsync(DateTime beforeUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<OverdueBusinessDay>> FindOpenDaysAsync(DateOnly beforeDate, CancellationToken cancellationToken);
    Task<OverdueTransfer?> FindTransferAsync(Guid id, bool lockForProcessing, CancellationToken cancellationToken);
    Task<OverdueBusinessDay?> FindDayAsync(Guid id, bool lockForProcessing, CancellationToken cancellationToken);
}
