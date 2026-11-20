using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Domain.Rules;

// Cifras de facturación actuales. El cierre guarda una copia de este resultado.
public static class InvoiceSalesCalculation
{
    public static BusinessDayClosing Calculate(IReadOnlyList<Invoice> invoices, IReadOnlySet<Guid> pendingCancellations)
    {
        var included = invoices.Where(invoice => !pendingCancellations.Contains(invoice.Id)
            && invoice.State is InvoiceState.Paid or InvoiceState.InDebt or InvoiceState.PendingVerification).ToArray();
        var payments = included.SelectMany(invoice => invoice.Payments)
            .Where(payment => payment.Kind == PaymentKind.Sale).ToArray();

        return new BusinessDayClosing(included.Sum(invoice => invoice.Total),
            payments.Where(payment => payment.Method == PaymentMethod.Cash && payment.Status == PaymentStatus.Confirmed)
                .Sum(payment => payment.Amount),
            payments.Where(payment => payment.Method == PaymentMethod.Transfer && payment.Status == PaymentStatus.Confirmed)
                .Sum(payment => payment.Amount),
            payments.Where(payment => payment.Method == PaymentMethod.Transfer && payment.Status == PaymentStatus.Registered)
                .Sum(payment => payment.Amount),
            included.Where(invoice => invoice.DebtorName is not null).Sum(invoice => invoice.Total - invoice.Payments
                .Where(payment => payment.Kind == PaymentKind.Sale && payment.Status != PaymentStatus.Rejected)
                .Sum(payment => payment.Amount)),
            invoices.Where(invoice => pendingCancellations.Contains(invoice.Id)).Sum(invoice => invoice.Total),
            invoices.Where(invoice => !pendingCancellations.Contains(invoice.Id) && invoice.State == InvoiceState.Cancelled)
                .Sum(invoice => invoice.Total),
            invoices.Where(invoice => !pendingCancellations.Contains(invoice.Id) && invoice.State == InvoiceState.NotDelivered)
                .Sum(invoice => invoice.Total));
    }
}
