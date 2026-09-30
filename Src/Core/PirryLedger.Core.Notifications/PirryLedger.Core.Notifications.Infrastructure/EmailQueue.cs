using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Domain;

namespace PirryLedger.Core.Notifications.Infrastructure;

// Lo unico autorizado para escribir en la cola (RF-NOT-08). La operacion que
// origina el correo llama a EnqueueAsync y termina: no abre conexion SMTP, no
// espera al servidor de correo y no falla porque el servidor no responda.
//
// Guarda de inmediato. El orden que sigue el modulo que la llama es escribir su
// propio cambio primero y encolar despues, para no dejar un correo de activacion
// de un usuario que al final no se guardo.
internal sealed class EmailQueue : IEmailQueue
{
    private readonly NotificationsDbContext _contexto;
    private readonly IClock _reloj;

    public EmailQueue(NotificationsDbContext contexto, IClock reloj)
    {
        _contexto = contexto;
        _reloj = reloj;
    }

    public async Task EnqueueAsync(QueuedEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        var correo = CorreoEnCola.Crear(email.Destinatario, email.Asunto, email.Cuerpo, _reloj.UtcNow);

        _contexto.CorreosEnCola.Add(correo);

        await _contexto.SaveChangesAsync(cancellationToken);
    }
}
