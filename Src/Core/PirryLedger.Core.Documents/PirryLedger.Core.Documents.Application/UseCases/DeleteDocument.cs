using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Documents.Application.Exceptions;
using PirryLedger.Core.Documents.Application.Ports;

namespace PirryLedger.Core.Documents.Application.UseCases;

// RF-DOC-04: eliminar un documento de forma logica. No se borra el registro ni
// el objeto: se marca la fecha y quien lo hizo, y deja de aparecer en el
// listado.
public sealed class DeleteDocument
{
    private readonly IDocumentRepository _repository;
    private readonly Authenticate _authenticate;
    private readonly IClock _clock;

    public DeleteDocument(
        IDocumentRepository repository,
        Authenticate authenticate,
        IClock clock)
    {
        _repository = repository;
        _authenticate = authenticate;
        _clock = clock;
    }

    public async Task ExecuteAsync(
        string plainToken,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var caller = await _authenticate.ExecuteAsync(plainToken, cancellationToken);

        var document = await _repository.FindAsync(documentId, cancellationToken)
            ?? throw new DocumentNotFoundException();

        if (document.IsDeleted)
        {
            throw new DocumentNotFoundException();
        }

        DocumentOwnership.EnsureCanAccess(caller, document);

        document.Delete(caller.Id, _clock.UtcNow);

        await _repository.SaveAsync(document, cancellationToken);
    }
}
