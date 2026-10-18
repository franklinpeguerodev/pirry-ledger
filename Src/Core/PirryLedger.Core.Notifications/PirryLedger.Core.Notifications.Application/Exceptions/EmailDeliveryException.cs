using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;
namespace PirryLedger.Core.Notifications.Application;

// failure al entregar un Email. El emisor la captura, devuelve el Email a
// Pending y sigue con los demas. El message es util para quien lanza el
// emisor desde la consola y nunca se devuelve en una respuesta HTTP (RD-08).
public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException(string message)
        : base(message)
    {
    }

    public EmailDeliveryException(string message, Exception causa)
        : base(message, causa)
    {
    }
}
