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

    // Una sola puerta de entrada: nada escribe la fila directamente.
    public static EmailQueueItem Create(string recipient, string subject, string body, DateTime createdAtUtc)
    {
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
    public void Claim()
    {
        if (Status != EmailStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Solo un Email Pending se puede reclamar. Status actual: {Status}.");
        }

        Status = EmailStatus.Processing;
        Attempts++;
    }

    public void MarkSent(DateTime utcNow)
    {
        if (Status != EmailStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Solo un Email Processing se puede marcar Sent. Status actual: {Status}.");
        }

        Status = EmailStatus.Sent;
        SentAtUtc = utcNow;
    }

    // El transporte falla. El Email vuelve a Pending para que la proxima
    // ejecucion del emisor lo intente otra vez. No hay reintento automatico:
    // el emisor corre cuando alguien lo lanza.
    public void Release()
    {
        if (Status != EmailStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Solo un Email Processing se puede Release. Status actual: {Status}.");
        }

        Status = EmailStatus.Pending;
    }
}
