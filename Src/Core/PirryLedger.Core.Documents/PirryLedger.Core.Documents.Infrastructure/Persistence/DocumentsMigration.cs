using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Core.Documents.Infrastructure.Persistence;

// ADR 018: el paso de migracion de la pieza de documentos.
internal sealed class DocumentsMigration : IModuleMigration
{
    private readonly DocumentDbContext _context;

    public DocumentsMigration(DocumentDbContext context)
    {
        _context = context;
    }

    public string Name => "Documents";

    public Task ApplyAsync(CancellationToken cancellationToken) =>
        _context.Database.MigrateAsync(cancellationToken);
}
