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

    // RF-NOT-12. El reclamo es una sola sentencia: UPDATE con un subquery que
    // pide filas con FOR UPDATE SKIP LOCKED. Si dos procesos ejecutan el emisor
    // a la vez, el primero marca sus filas como Processing y el segundo, al
    // mirar, ya no las ve como Pending. Por eso el segundo no puede duplicar
    // ningun envio, y por eso el Status cambia en la base y no despues en
    // memoria.
    public async Task<IReadOnlyList<EmailQueueItem>> ClaimPendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        var emails = await _context.EmailQueueItems
            .FromSqlInterpolated($$"""
                UPDATE not_email_queue
                SET status = {{(int)EmailStatus.Processing}},
                    attempts = attempts + 1
                WHERE id IN (
                    SELECT id
                    FROM not_email_queue
                    WHERE status = {{(int)EmailStatus.Pending}}
                    ORDER BY created_at_utc
                    FOR UPDATE SKIP LOCKED
                    LIMIT {{limit}}
                )
                RETURNING *;
                """)
            .ToListAsync(cancellationToken);

        return emails;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EmailQueueItems
            .CountAsync(email => email.Status == EmailStatus.Pending, cancellationToken);
    }
}
