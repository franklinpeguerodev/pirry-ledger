namespace PirryLedger.Core.Contracts.Time;

// La unica implementacion: el reloj real. Los modulos reciben IClock por
// inyeccion, nunca esta clase directamente.
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
