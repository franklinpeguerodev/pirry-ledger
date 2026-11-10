using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Documents.Application.Ports;
using PirryLedger.Core.Documents.Domain.Entities;

namespace PirryLedger.Core.Documents.Infrastructure.Persistence;

// RF-DOC-01..04: la persistencia del registro. Los listados excluyen los
// eliminados en la consulta (RF-DOC-02) y ordenan del mas reciente al mas
// antiguo, que es como se lee un listado de documentos.
internal sealed class DocumentRepository : IDocumentRepository
{
    private readonly DocumentDbContext _context;

    public DocumentRepository(DocumentDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(Document document, CancellationToken cancellationToken = default)
    {
        // Update sirve para la subida y para la eliminacion logica: la entidad
        // ya trae su Id y el contexto la marca entera.
        _context.Documents.Update(document);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<Document?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Documents.FirstOrDefaultAsync(document => document.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Document>> ListByOwnerAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default) =>
        await _context.Documents
            .AsNoTracking()
            .Where(document => document.UploadedByUserId == ownerId && document.DeletedAtUtc == null)
            .OrderByDescending(document => document.UploadedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Document>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Documents
            .AsNoTracking()
            .Where(document => document.DeletedAtUtc == null)
            .OrderByDescending(document => document.UploadedAtUtc)
            .ToListAsync(cancellationToken);
}
