namespace PirryLedger.Core.Notifications.Domain.Entities;

public sealed class OverdueNotificationRecord
{
    private OverdueNotificationRecord() { }
    public Guid Id { get; private set; }
    public string EventKey { get; private set; } = string.Empty;
    public DateTime RecordedAtUtc { get; private set; }

    public static OverdueNotificationRecord Create(string eventKey, DateTime recordedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(eventKey) || eventKey.Length > 160)
            throw new ArgumentException("La clave de atraso debe contener entre 1 y 160 caracteres.");
        if (recordedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha de registro debe estar en UTC.");
        return new OverdueNotificationRecord { Id = Guid.NewGuid(), EventKey = eventKey, RecordedAtUtc = recordedAtUtc };
    }
}
