namespace PirryLedger.Business.Application.Exceptions;

public sealed class InvoiceRecoveryDeniedException : Exception
{
    public InvoiceRecoveryDeniedException() : base("Solo el Estándar emisor puede recuperar esta factura.")
    {
    }
}
