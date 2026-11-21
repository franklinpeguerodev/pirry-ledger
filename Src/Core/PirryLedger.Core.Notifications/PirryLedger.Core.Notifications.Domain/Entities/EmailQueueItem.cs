using PirryLedger.Core.Notifications.Domain.Enums;

namespace PirryLedger.Core.Notifications.Domain.Entities;

// RF-NOT-08 y RF-NOT-09: el Email no sale de la operacion que lo origina, queda
// en esta fila como Pending y un proceso aparte lo entrega.
//
// Atributos minimos exigidos por docs/requirements/entities.md: Recipient,
// Subject, Body, Status, Attempts, fecha de creacion, fecha de envio y ultimo
// error. Los tres ultimos se crean vacios y los rellena el emisor.
//
// La regla de transiciones vive aqui, en la entidad (RD-04): de Sent no se
// sale, y de Sent a Sent tampoco, que es lo que hace imposible el envio
// duplicado de RF-NOT-12.
public sealed class EmailQueueItem
{
    public const int MaximumAttempts = 5;
    private EmailQueueItem()
    {
    }

    private EmailQueueItem(Guid id, string recipient, string subject, string body, DateTime createdAtUtc)
    {
        Id = id;
        Recipient = recipient;
        Subject = subject;
        Body = body;
        Status = EmailStatus.Pending;
        Attempts = 0;
        CreatedAtUtc = createdAtUtc;
        SentAtUtc = null;
        LastError = null;
        StatusChangedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Recipient { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public EmailStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? SentAtUtc { get; private set; }

    public string? LastError { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public DateTime? StatusChangedAtUtc { get; private set; }
    public Guid? NotificationId { get; private set; }
    public Guid? NotificationRecipientUserId { get; private set; }

    public static EmailQueueItem ForNotification(Guid notificationId, Guid recipientUserId,
        string recipient, string subject, string body, DateTime createdAtUtc)
    {
        if (notificationId == Guid.Empty || recipientUserId == Guid.Empty)
            throw new ArgumentException("El correo notificable requiere aviso y destinatario.");
        var email = Create(recipient, subject, body, createdAtUtc);
        email.NotificationId = notificationId;
        email.NotificationRecipientUserId = recipientUserId;
        return email;
    }

    public void CancelNotification(DateTime? changedAtUtc = null)
    {
        ValidateUtc(changedAtUtc);
        if (NotificationId is null || Status is not (EmailStatus.Pending or EmailStatus.Processing))
            throw new InvalidOperationException("Solo se puede cancelar un correo notificable pendiente.");
        Status = EmailStatus.Cancelled;
        StatusChangedAtUtc = changedAtUtc;
    }

    // Una sola puerta de entrada: nada escribe la fila directamente.
    public static EmailQueueItem Create(string recipient, string subject, string body, DateTime createdAtUtc)
    {
        ValidateUtc(createdAtUtc);
        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new ArgumentException("El destinatario es obligatorio.", nameof(recipient));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("El asunto es obligatorio.", nameof(subject));
        }

        return new EmailQueueItem(Guid.NewGuid(), recipient, subject, body, createdAtUtc);
    }

    // El emisor toma el Email. De Pending a Processing es el unico salto que
    // permite despues marcar Sent, asi que dos procesos que lean la cola a
    // la vez no pueden enviar el mismo Email.
    public void Claim(DateTime? startedAtUtc = null)
    {
        ValidateUtc(startedAtUtc);
        if (Status != EmailStatus.Pending || Attempts >= MaximumAttempts)
        {
            throw new InvalidOperationException(
                $"Solo un Email Pending se puede reclamar. Status actual: {Status}.");
        }

        Status = EmailStatus.Processing;
        Attempts++;
        LastAttemptAtUtc = startedAtUtc;
        StatusChangedAtUtc = startedAtUtc;
    }

    public void MarkSent(DateTime utcNow)
    {
        ValidateUtc(utcNow);
        if (Status != EmailStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Solo un Email Processing se puede marcar Sent. Status actual: {Status}.");
        }

        Status = EmailStatus.Sent;
        SentAtUtc = utcNow;
        StatusChangedAtUtc = utcNow;
    }

    // Un fallo conocido permite otro intento, salvo al agotar el máximo.
    public void Release(string safeError = "No se pudo entregar el correo.", DateTime? changedAtUtc = null)
    {
        ValidateUtc(changedAtUtc);
        if (Status != EmailStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Solo un Email Processing se puede Release. Status actual: {Status}.");
        }

        Status = Attempts >= MaximumAttempts ? EmailStatus.Failed : EmailStatus.Pending;
        LastError = EmailQueueErrors.Safe(safeError);
        StatusChangedAtUtc = changedAtUtc;
    }

    public void MarkUncertain(DateTime changedAtUtc)
    {
        ValidateUtc(changedAtUtc);
        if (Status != EmailStatus.Processing)
            throw new InvalidOperationException("Solo un correo en procesamiento puede quedar incierto.");
        Status = EmailStatus.Uncertain;
        LastError = EmailQueueErrors.Uncertain;
        StatusChangedAtUtc = changedAtUtc;
    }

    private static void ValidateUtc(DateTime? value)
    {
        if (value is { Kind: not DateTimeKind.Utc })
            throw new ArgumentException("Las fechas del correo deben estar en UTC.");
    }
}
