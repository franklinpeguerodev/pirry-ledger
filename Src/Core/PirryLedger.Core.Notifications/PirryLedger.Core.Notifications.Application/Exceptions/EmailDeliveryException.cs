using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;
namespace PirryLedger.Core.Notifications.Application;

// Resultado de transporte conocido o incierto. Mensajes controlados; el procesador
// vuelve a filtrar el texto antes de persistirlo o mostrarlo (RD-08).
public sealed class EmailDeliveryException : Exception
{
    public bool IsUncertain { get; }
    public EmailDeliveryException(string message)
        : base(message)
    {
    }

    public EmailDeliveryException(string message, Exception causa)
        : base(message, causa)
    {
    }

    public EmailDeliveryException(string safeMessage, bool isUncertain) : base(safeMessage)
    {
        IsUncertain = isUncertain;
    }
}
