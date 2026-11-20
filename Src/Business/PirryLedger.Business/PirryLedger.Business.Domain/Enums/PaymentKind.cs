namespace PirryLedger.Business.Domain.Enums;

// Distingue cobro original de venta y abono posterior, ambos con método.
// Los abonos no se incorporan al efectivo ni a las ventas del cierre.
public enum PaymentKind
{
    Sale,
    Debt,
}
