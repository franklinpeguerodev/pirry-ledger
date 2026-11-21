namespace PirryLedger.Core.Notifications.Domain.Entities;

public sealed class Notification
{
    private Notification() { }

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    public void MarkRead(DateTime readAtUtc)
    {
        if (readAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha de lectura debe estar en UTC.", nameof(readAtUtc));
        ReadAtUtc ??= readAtUtc;
    }

    public static Notification Create(Guid recipientUserId, string subject, string body, DateTime createdAtUtc)
    {
        if (recipientUserId == Guid.Empty)
            throw new ArgumentException("El destinatario es obligatorio.", nameof(recipientUserId));
        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > 200)
            throw new ArgumentException("El asunto debe contener entre 1 y 200 caracteres.", nameof(subject));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("El contenido del aviso es obligatorio.", nameof(body));
        if (createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha del aviso debe estar en UTC.", nameof(createdAtUtc));

        return new Notification
        {
            Id = Guid.NewGuid(), RecipientUserId = recipientUserId,
            Subject = subject.Trim(), Body = body.Trim(), CreatedAtUtc = createdAtUtc
        };
    }
}
