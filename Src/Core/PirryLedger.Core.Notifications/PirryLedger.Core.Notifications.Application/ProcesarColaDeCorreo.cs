using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.Notifications.Application;

// RF-NOT-09: este caso de uso es el proceso independiente que toma los correos
// pendientes y los entrega. Corre por fuera de la operacion que los creo.
//
// RF-NOT-12: cada correo se reclama una sola vez y solo un correo Enviado puede
// pasar a Enviado, asi que ejecutar este caso de uso dos veces seguidas no
// duplica ningun envio. La segunda pasada no encuentra nada Pendiente.
public sealed class ProcesarColaDeCorreo
{
    private const int LotePorDefecto = 50;

    private readonly ICorreoEnColaRepository _repositorio;
    private readonly IEmailTransporter _transporte;
    private readonly IClock _reloj;

    public ProcesarColaDeCorreo(
        ICorreoEnColaRepository repositorio,
        IEmailTransporter transporte,
        IClock reloj)
    {
        _repositorio = repositorio;
        _transporte = transporte;
        _reloj = reloj;
    }

    public async Task<ResultadoDelProcesamiento> EjecutarAsync(int lote = LotePorDefecto, CancellationToken cancellationToken = default)
    {
        if (lote <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lote), "El lote debe ser mayor que cero.");
        }

        var correos = await _repositorio.ReclamarPendientesAsync(lote, cancellationToken);

        var fallidos = new List<FalloDeEnvio>();

        foreach (var correo in correos)
        {
            try
            {
                await _transporte.EnviarAsync(correo.Destinatario, correo.Asunto, correo.Cuerpo, cancellationToken);

                correo.MarcarEnviado(_reloj.UtcNow);
            }
            catch (Exception excepcion)
            {
                // El correo vuelve a Pendiente para que la proxima ejecucion lo
                // intente. No hay reintento automatico: el emisor corre cuando
                // alguien lo lanza.
                correo.Liberar();

                fallidos.Add(new FalloDeEnvio(correo.Destinatario, excepcion.Message));
            }
        }

        await _repositorio.GuardarAsync(cancellationToken);

        return new ResultadoDelProcesamiento(correos.Count, correos.Count - fallidos.Count, fallidos);
    }
}
