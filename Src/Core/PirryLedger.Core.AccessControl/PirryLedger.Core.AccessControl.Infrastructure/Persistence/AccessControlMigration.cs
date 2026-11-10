using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Core.AccessControl.Infrastructure.Persistence;

// ADR 018: el paso de migracion de AccessControl. Vive en la pieza para que el
// Host no tenga que conocer su DbContext.
internal sealed class AccessControlMigration : IModuleMigration
{
    private readonly AccessControlDbContext _context;

    public AccessControlMigration(AccessControlDbContext context)
    {
        _context = context;
    }

    public string Name => "AccessControl";

    public Task ApplyAsync(CancellationToken cancellationToken) =>
        _context.Database.MigrateAsync(cancellationToken);
}
