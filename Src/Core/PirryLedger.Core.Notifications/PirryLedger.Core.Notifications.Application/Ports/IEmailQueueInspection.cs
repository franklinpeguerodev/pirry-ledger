using PirryLedger.Core.Notifications.Domain.Enums;

namespace PirryLedger.Core.Notifications.Application;

public sealed record EmailQueueFilter(EmailStatus? Status = null, DateTime? FromUtc = null,
    DateTime? ToUtc = null, int Offset = 0, int Limit = 50);

public sealed record EmailQueueSummary(Guid Id, string Recipient, string Subject, string Status,
    int Attempts, DateTime CreatedAtUtc, DateTime? SentAtUtc, DateTime? LastAttemptAtUtc,
    DateTime? StatusChangedAtUtc, string? LastError);

public sealed record EmailQueuePage(IReadOnlyList<EmailQueueSummary> Items, int Total);

public interface IEmailQueueInspection
{
    Task<EmailQueuePage> ListAsync(EmailQueueFilter filter, CancellationToken cancellationToken = default);
}
