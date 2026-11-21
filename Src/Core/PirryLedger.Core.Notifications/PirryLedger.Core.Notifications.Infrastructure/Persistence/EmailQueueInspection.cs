using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Infrastructure;

public sealed class EmailQueueInspection(NotificationsDbContext context) : IEmailQueueInspection
{
    public async Task<EmailQueuePage> ListAsync(EmailQueueFilter filter, CancellationToken cancellationToken = default)
    {
        var query = context.EmailQueueItems.AsNoTracking();
        if (filter.Status is { } status) query = query.Where(email => email.Status == status);
        if (filter.FromUtc is { } from) query = query.Where(email => email.CreatedAtUtc >= from);
        if (filter.ToUtc is { } to) query = query.Where(email => email.CreatedAtUtc <= to);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(email => email.CreatedAtUtc).ThenBy(email => email.Id)
            .Skip(filter.Offset).Take(filter.Limit)
            .Select(email => new EmailQueueSummary(email.Id, email.Recipient, email.Subject, email.Status.ToString(),
                email.Attempts, email.CreatedAtUtc, email.SentAtUtc, email.LastAttemptAtUtc,
                email.StatusChangedAtUtc, email.LastError)).ToListAsync(cancellationToken);
        return new EmailQueuePage(rows.Select(row => row with { LastError = EmailQueueErrors.Safe(row.LastError) }).ToArray(), total);
    }
}
