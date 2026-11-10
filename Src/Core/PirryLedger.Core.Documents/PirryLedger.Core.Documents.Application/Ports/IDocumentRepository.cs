using PirryLedger.Core.Documents.Domain.Entities;

namespace PirryLedger.Core.Documents.Application.Ports;

// RF-DOC-01..04: el registro de los documentos. El repositorio nunca devuelve
// eliminados en los listados: RF-DOC-02 pide que el listado no los incluya.
public interface IDocumentRepository
{
    Task SaveAsync(Document document, CancellationToken cancellationToken = default);

    // Devuelve null si no existe. Incluye los eliminados: el caso de uso decide
    // que un eliminado es "no encontrado" al descargar o al volver a eliminar.
    Task<Document?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> ListAllAsync(CancellationToken cancellationToken = default);
}
