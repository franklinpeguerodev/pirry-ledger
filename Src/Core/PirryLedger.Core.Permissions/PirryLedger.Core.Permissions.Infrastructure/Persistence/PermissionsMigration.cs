using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Core.Permissions.Infrastructure.Persistence;

// ADR 018: el paso de migracion de Permissions.
internal sealed class PermissionsMigration : IModuleMigration
{
    private readonly PermissionsDbContext _context;

    public PermissionsMigration(PermissionsDbContext context)
    {
        _context = context;
    }

    public string Name => "Permissions";

    public Task ApplyAsync(CancellationToken cancellationToken) =>
        _context.Database.MigrateAsync(cancellationToken);
}
