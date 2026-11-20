using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Application.UseCases;

// Vista operativa: sin historial, fechas de abonos ni datos del registrador.
public sealed record DebtSummary(
    Guid InvoiceId,
    Guid BranchId,
    string DebtorName,
    DateTime IssuedAtUtc,
    decimal Total,
    decimal OutstandingAmount,
    bool HasPendingTransfer,
    bool IsSettled)
{
    public static DebtSummary From(Invoice invoice) => new(
        invoice.Id,
        invoice.BranchId,
        invoice.DebtorName!,
        invoice.IssuedAtUtc,
        invoice.Total,
        invoice.Total - invoice.ConfirmedPaidAmount,
        invoice.HasPendingTransfer,
        invoice.State == InvoiceState.Paid);
}
