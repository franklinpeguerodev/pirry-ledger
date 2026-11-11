using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Business.Infrastructure.Persistence;

// ADR 018: el paso de migracion del modulo de negocio.
internal sealed class BusinessMigration : IModuleMigration
{
    private readonly BusinessDbContext _context;

    public BusinessMigration(BusinessDbContext context)
    {
        _context = context;
    }

    public string Name => "Business";

    public Task ApplyAsync(CancellationToken cancellationToken) =>
        _context.Database.MigrateAsync(cancellationToken);
}
