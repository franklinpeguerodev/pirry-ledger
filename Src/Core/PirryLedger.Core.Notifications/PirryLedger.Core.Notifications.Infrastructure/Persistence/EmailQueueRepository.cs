using PirryLedger.Core.Notifications.Application;
using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;

namespace PirryLedger.Core.Notifications.Infrastructure;

internal sealed class EmailQueueRepository : IEmailQueueRepository
{
    private readonly NotificationsDbContext _context;

    public EmailQueueRepository(NotificationsDbContext context)
    {
        _context = context;
    }

    // El lote es una selección; cada reclamo posterior verifica estado y límite
    // en PostgreSQL. El bloqueo del procesador coordina recuperación y envíos.
    public async Task<IReadOnlyList<EmailQueueItem>> ListPendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        ResetTracking();
        return await _context.EmailQueueItems.AsNoTracking()
            .Where(email => email.Status == EmailStatus.Pending && email.Attempts < EmailQueueItem.MaximumAttempts)
            .OrderBy(email => email.CreatedAtUtc).ThenBy(email => email.Id).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<EmailQueueItem?> ClaimAsync(Guid id, DateTime attemptedAtUtc, CancellationToken cancellationToken = default)
    {
        ResetTracking();
        var emails = await _context.EmailQueueItems
            .FromSqlInterpolated($$"""
                UPDATE not_email_queue
                SET status = {{(int)EmailStatus.Processing}},
                    attempts = attempts + 1,
                    last_attempt_at_utc = {{attemptedAtUtc}},
                    status_changed_at_utc = {{attemptedAtUtc}}
                WHERE id = {{id}} AND status = {{(int)EmailStatus.Pending}}
                    AND attempts < {{EmailQueueItem.MaximumAttempts}}
                RETURNING *;
                """)
            .ToListAsync(cancellationToken);

        return emails.SingleOrDefault();
    }

    public Task<int> RecoverInterruptedAsync(DateTime changedAtUtc, CancellationToken cancellationToken = default)
    {
        ResetTracking();
        return _context.EmailQueueItems.Where(email => email.Status == EmailStatus.Processing)
            .ExecuteUpdateAsync(update => update.SetProperty(email => email.Status, EmailStatus.Uncertain)
                .SetProperty(email => email.LastError, EmailQueueErrors.Uncertain)
                .SetProperty(email => email.StatusChangedAtUtc, changedAtUtc), cancellationToken);
    }

    public async Task CancelPendingNotificationAsync(Guid id, DateTime changedAtUtc, CancellationToken cancellationToken = default)
    {
        await _context.EmailQueueItems.Where(email => email.Id == id && email.Status == EmailStatus.Pending && email.NotificationId != null)
            .ExecuteUpdateAsync(update => update.SetProperty(email => email.Status, EmailStatus.Cancelled)
                .SetProperty(email => email.StatusChangedAtUtc, changedAtUtc), cancellationToken);
    }

    private void ResetTracking()
    {
        if (_context.ChangeTracker.HasChanges())
            throw new InvalidOperationException("El procesador debe iniciar sin cambios pendientes de otra operación.");
        _context.ChangeTracker.Clear();
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EmailQueueItems
            .CountAsync(email => email.Status == EmailStatus.Pending && email.Attempts < EmailQueueItem.MaximumAttempts, cancellationToken);
    }
}
