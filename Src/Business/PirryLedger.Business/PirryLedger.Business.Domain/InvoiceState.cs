namespace PirryLedger.Business.Domain;

// RF-NEG-03: the business state set is declared in one place.
public enum InvoiceState
{
    Draft,
    Issued,
    Paid,
    Cancelled,
}
