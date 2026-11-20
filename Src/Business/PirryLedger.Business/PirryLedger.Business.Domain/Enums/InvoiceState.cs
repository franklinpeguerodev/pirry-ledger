namespace PirryLedger.Business.Domain.Enums;

// RF-NEG-03: estados del negocio declarados en un solo lugar.
// Issued se muestra como "pendiente". Cancelled es terminal (RF-NEG-05).
// NotDelivered admite recuperación explícita (ADR 028).
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
