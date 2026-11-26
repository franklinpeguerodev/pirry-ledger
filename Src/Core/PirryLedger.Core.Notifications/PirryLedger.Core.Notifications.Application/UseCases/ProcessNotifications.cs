namespace PirryLedger.Core.Notifications.Application;

public sealed record NotificationProcessingResult(OverdueProcessingResult Overdue, ProcessingResult Email);

public sealed class ProcessNotifications(ProcessOverdueNotifications overdue, ProcessEmailQueue email)
{
    public async Task<NotificationProcessingResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var reminders = await overdue.ExecuteAsync(cancellationToken);
        var sent = await email.ExecuteAsync(cancellationToken: cancellationToken);
        return new(reminders, sent);
    }
}
