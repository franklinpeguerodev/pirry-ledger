using PirryLedger.Core.Documents.Domain.Entities;

namespace PirryLedger.Core.Documents.Application.Ports;

// RF-DOC-01..04: el registro de los documentos. El repositorio nunca devuelve
// eliminados en los listados: RF-DOC-02 pide que el listado no los incluya.
//
// Alta y cambio son operaciones distintas a proposito. La entidad trae su Id
// puesto (lo genera el dominio), asi que "guardar" no basta: un alta marcada
// como cambio lanza DbUpdateConcurrencyException porque la fila no existe. Es
// lo que hace el modulo de negocio: Add para el alta y Update para el cambio.
public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken cancellationToken = default);

    Task UpdateAsync(Document document, CancellationToken cancellationToken = default);

    // Devuelve null si no existe. Incluye los eliminados: el caso de uso decide
    // que un eliminado es "no encontrado" al descargar o al volver a eliminar.
    Task<Document?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> ListAllAsync(CancellationToken cancellationToken = default);
}
