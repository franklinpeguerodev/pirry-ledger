using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;

namespace PirryLedger.Core.Notifications.Application;

// Salida del emisor. Lleva el detalle de los failures para que quien lo lanzo (la
// consola) pueda ver por que failure un Email, sin que eso llegue nunca a una
// respuesta HTTP (RD-08).
public sealed record DeliveryFailure(string Recipient, string Error);

public sealed record ProcessingResult(
    int Pending,
    int Sent,
    IReadOnlyList<DeliveryFailure> Failures)
{
    public int Total => Pending;
}
