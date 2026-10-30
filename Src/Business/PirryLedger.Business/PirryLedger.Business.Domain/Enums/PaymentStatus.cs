namespace PirryLedger.Business.Domain.Enums;

// El efectivo nace confirmado. Una transferencia nace Registered y el
// Administrador la confirma o la rechaza; una transferencia rechazada no cuenta
// para el total pagado.
public enum PaymentStatus
{
    Registered,
    Confirmed,
    Rejected,
}
