namespace PirryLedger.Business.Domain.Enums;

// RF-NEG-03: the business state set is declared in one place.
// Issued is shown to the user as "pendiente".
// Cancelled and NotDelivered are terminal (RF-NEG-05).
public enum InvoiceState
{
    Draft,
    Issued,
    PendingVerification,
    Paid,
    InDebt,
    Cancelled,
    NotDelivered,
}
