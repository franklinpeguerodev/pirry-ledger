using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Infrastructure;

// Lo unico autorizado para escribir en la cola (RF-NOT-08). La operacion que
// origina el Email llama a EnqueueAsync y termina: no abre conexion SMTP, no
// espera al servidor de Email y no falla porque el servidor no responda.
//
// Guarda de inmediato. El orden que sigue el modulo que la llama es escribir su
// propio cambio primero y encolar despues, para no dejar un Email de activacion
// de un User que al final no se guardo.
internal sealed class EmailQueue : IEmailQueue
{
    private readonly NotificationsDbContext _context;
    private readonly IClock _clock;

    public EmailQueue(NotificationsDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task EnqueueAsync(QueuedEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        var item = EmailQueueItem.Create(email.Recipient, email.Subject, email.Body, _clock.UtcNow);

        _context.EmailQueueItems.Add(item);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
