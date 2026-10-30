namespace PirryLedger.Business.Domain.Enums;

// Un pago de venta se cobra en el mostrador; un abono de deuda lo registra un
// Administrador y no tiene metodo.
public enum PaymentKind
{
    Sale,
    Debt,
}
