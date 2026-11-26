using PirryLedger.Core.Notifications.Domain.Enums;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Application;

// RF-NOT-09: este caso de uso es el proceso independiente que toma los emails
// Pending y los entrega. Corre por fuera de la operacion que los creo.
//
// RF-NOT-12: enviados e inciertos nunca se reclaman de nuevo. El bloqueo de
// coordinación aísla ejecuciones; cada resultado se guarda antes del siguiente.
public sealed class ProcessEmailQueue
{
    private const int DefaultBatch = 50;

    private readonly IEmailQueueRepository _repository;
    private readonly IEmailTransporter _transporter;
    private readonly IClock _clock;
    private readonly INotificationRecipientDirectory? _directory;
    private readonly INotificationDeliveryStore? _delivery;
    private readonly IEmailQueueProcessingLock _processingLock;
    private readonly INotificationEmailEligibility _eligibility;

    public ProcessEmailQueue(
        IEmailQueueRepository repository,
        IEmailTransporter transporter,
        IClock clock,
        IEmailQueueProcessingLock processingLock,
        INotificationEmailEligibility eligibility,
        INotificationRecipientDirectory? directory = null,
        INotificationDeliveryStore? delivery = null)
    {
        _repository = repository;
        _transporter = transporter;
        _clock = clock;
        _directory = directory;
        _delivery = delivery;
        _processingLock = processingLock ?? throw new ArgumentNullException(nameof(processingLock));
        _eligibility = eligibility;
    }

    public async Task<ProcessingResult> ExecuteAsync(int batch = DefaultBatch, CancellationToken cancellationToken = default)
    {
        if (batch <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batch), "El lote debe ser mayor que cero.");
        }

        await using var lease = await _processingLock.TryAcquireAsync(cancellationToken);
        if (lease is null) return new ProcessingResult(0, 0, [], AlreadyRunning: true);
        await lease.VerifyAsync(cancellationToken);
        var uncertain = await _repository.RecoverInterruptedAsync(_clock.UtcNow, cancellationToken);
        var emails = await _repository.ListPendingAsync(batch, cancellationToken);
        var failures = new List<DeliveryFailure>();
        var sent = 0;
        var attempted = 0;
        var failed = 0;
        var cancelled = 0;

        foreach (var pending in emails)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await lease.VerifyAsync(cancellationToken);
            if (pending.NotificationId is not null)
            {
                if (_directory is null || _delivery is null || pending.NotificationRecipientUserId is not { } recipientId)
                    throw new InvalidOperationException("No se puede verificar el destinatario del correo notificable.");
                var recipient = await _directory.FindActiveAsync(recipientId, cancellationToken);
                if (recipient is not { IsAdministrator: true }
                    || !await _delivery.EmailEnabledAsync(recipientId, cancellationToken)
                    || !await _eligibility.IsRelevantAsync(pending.NotificationId.Value, cancellationToken))
                {
                    await _repository.CancelPendingNotificationAsync(pending.Id, _clock.UtcNow, cancellationToken);
                    cancelled++;
                    continue;
                }
            }
            await lease.VerifyAsync(cancellationToken);
            var email = await _repository.ClaimAsync(pending.Id, _clock.UtcNow, cancellationToken);
            if (email is null) continue;
            attempted++;
            try
            {
                await _transporter.SendAsync(email.Recipient, email.Subject, email.Body, cancellationToken);
                email.MarkSent(_clock.UtcNow);
                sent++;
            }
            catch (EmailDeliveryException exception) when (!exception.IsUncertain)
            {
                email.Release(exception.Message, _clock.UtcNow);
                if (email.Status == EmailStatus.Failed) failed++;
                failures.Add(new DeliveryFailure(email.Recipient, email.LastError!));
            }
            catch (Exception)
            {
                // Un resultado desconocido nunca autoriza repetir la entrega.
                email.MarkUncertain(_clock.UtcNow);
                uncertain++;
            }
            // Si falla guardar después de SMTP, abortar: no convertir Sent en fallo.
            await _repository.SaveAsync(CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return new ProcessingResult(attempted, sent, failures, uncertain, failed, cancelled);
    }
}
