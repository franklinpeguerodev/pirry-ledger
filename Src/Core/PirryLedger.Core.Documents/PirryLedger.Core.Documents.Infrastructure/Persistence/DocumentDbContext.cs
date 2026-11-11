using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Documents.Domain.Entities;

namespace PirryLedger.Core.Documents.Infrastructure.Persistence;

// Contexto propio de la pieza de documentos. Es duenia de sus tablas
// (doc_*): no ve las de AccessControl (ac_*), Notifications (not_*) ni
// Permissions (per_*), y ellas no ven esta (RD-01).
//
// El vinculo con otras piezas ocurre por identidad (ids) y por los contratos de
// PirryLedger.Core.Contracts, nunca por relaciones entre tablas: por eso no hay
// claves foraneas hacia ac_users ni hacia las sucursales.
public sealed class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentDbContext).Assembly);
    }
}
