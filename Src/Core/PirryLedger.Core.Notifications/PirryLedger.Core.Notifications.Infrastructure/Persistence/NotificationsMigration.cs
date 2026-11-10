using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Core.Notifications.Infrastructure.Persistence;

// ADR 018: el paso de migracion de Notifications.
internal sealed class NotificationsMigration : IModuleMigration
{
    private readonly NotificationsDbContext _context;

    public NotificationsMigration(NotificationsDbContext context)
    {
        _context = context;
    }

    public string Name => "Notifications";

    public Task ApplyAsync(CancellationToken cancellationToken) =>
        _context.Database.MigrateAsync(cancellationToken);
}
