namespace PirryLedger.Core.Notifications.Application;

// Fallo al entregar un correo. El emisor la captura, devuelve el correo a
// Pendiente y sigue con los demas. El mensaje es util para quien lanza el
// emisor desde la consola y nunca se devuelve en una respuesta HTTP (RD-08).
public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException(string mensaje)
        : base(mensaje)
    {
    }

    public EmailDeliveryException(string mensaje, Exception causa)
        : base(mensaje, causa)
    {
    }
}
