namespace PirryLedger.Core.Notifications.Application;

// Salida del emisor. Lleva el detalle de los fallos para que quien lo lanzo (la
// consola) pueda ver por que fallo un correo, sin que eso llegue nunca a una
// respuesta HTTP (RD-08).
public sealed record FalloDeEnvio(string Destinatario, string Error);

public sealed record ResultadoDelProcesamiento(
    int Pendientes,
    int Enviados,
    IReadOnlyList<FalloDeEnvio> Fallidos)
{
    public int Total => Pendientes;
}
