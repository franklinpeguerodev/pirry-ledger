namespace PirryLedger.Business.Application.Exceptions;

// La imagen que se sube pasa del limite de bytes. Es un rechazo distinto del
// tipo no admitido: aqui el archivo es una imagen valida, solo que demasiado
// grande. El endpoint lo traduce a 413.
public sealed class ProductImageTooLargeException : Exception
{
    public ProductImageTooLargeException(long maxBytes)
        : base($"La imagen no puede pasar de {maxBytes} bytes.")
    {
        MaxBytes = maxBytes;
    }

    public long MaxBytes { get; }
}
