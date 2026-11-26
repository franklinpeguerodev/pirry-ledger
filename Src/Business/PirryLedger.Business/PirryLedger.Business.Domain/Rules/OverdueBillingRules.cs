using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Domain.Rules;

public static class OverdueBillingRules
{
    public static bool IsTransferDue(PaymentMethod method, PaymentStatus status, InvoiceState invoiceState,
        DateTime registeredAtUtc, DateTime nowUtc)
    {
        BusinessDayCalendar.EnsureUtc(nowUtc);
        BusinessDayCalendar.EnsureUtc(registeredAtUtc);
        return method == PaymentMethod.Transfer && status == PaymentStatus.Registered
            && invoiceState != InvoiceState.Cancelled && nowUtc - registeredAtUtc > TimeSpan.FromDays(4);
    }

    public static bool IsBusinessDayDue(bool isOpen, DateOnly businessDate, DateTime nowUtc)
    {
        BusinessDayCalendar.EnsureUtc(nowUtc);
        return isOpen && nowUtc >= BusinessDayCalendar.IssueDeadline(businessDate);
    }
}
