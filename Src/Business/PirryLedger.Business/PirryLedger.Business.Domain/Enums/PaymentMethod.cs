namespace PirryLedger.Business.Domain.Enums;

public enum PaymentMethod
{
    Cash,
    Transfer,

    // Un abono de deuda se registra con monto y fecha, sin metodo (decision 7 de
    // current-iteration.md). None expresa "no aplica", no un medio de pago.
    None,
}
