using PirryLedger.Core.Notifications.Domain.Enums;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Application;

// RF-NOT-09: este caso de uso es el proceso independiente que toma los emails
// Pending y los entrega. Corre por fuera de la operacion que los creo.
//
// RF-NOT-12: cada Email se reclama una sola vez y solo un Email Sent puede
// pasar a Sent, asi que ejecutar este caso de uso dos veces seguidas no
// duplica ningun envio. La segunda pasada no encuentra nada Pending.
public sealed class ProcessEmailQueue
{
    private const int DefaultBatch = 50;

    private readonly IEmailQueueRepository _repository;
    private readonly IEmailTransporter _transporter;
    private readonly IClock _clock;

    public ProcessEmailQueue(
        IEmailQueueRepository repository,
        IEmailTransporter transporter,
        IClock clock)
    {
        _repository = repository;
        _transporter = transporter;
        _clock = clock;
    }

    public async Task<ProcessingResult> ExecuteAsync(int batch = DefaultBatch, CancellationToken cancellationToken = default)
    {
        if (batch <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batch), "El lote debe ser mayor que cero.");
        }

        var emails = await _repository.ClaimPendingAsync(batch, cancellationToken);

        var failures = new List<DeliveryFailure>();

        foreach (var email in emails)
        {
            try
            {
                await _transporter.SendAsync(email.Recipient, email.Subject, email.Body, cancellationToken);

                email.MarkSent(_clock.UtcNow);
            }
            catch (Exception exception)
            {
                // El Email vuelve a Pending para que la proxima ejecucion lo
                // intente. No hay reintento automatico: el emisor corre cuando
                // alguien lo lanza.
                email.Release();

                failures.Add(new DeliveryFailure(email.Recipient, exception.Message));
            }
        }

        await _repository.SaveAsync(cancellationToken);

        return new ProcessingResult(emails.Count, emails.Count - failures.Count, failures);
    }
}
