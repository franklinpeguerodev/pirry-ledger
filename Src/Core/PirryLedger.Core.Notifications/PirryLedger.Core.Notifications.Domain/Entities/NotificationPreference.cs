namespace PirryLedger.Core.Notifications.Domain.Entities;

public sealed class NotificationPreference
{
    public const bool DefaultEmailEnabled = true;
    // EF materializa esta fila; la escritura se realiza mediante upsert transaccional.
    private NotificationPreference() { }
    public Guid UserId { get; private set; }
    public bool EmailEnabled { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
}
