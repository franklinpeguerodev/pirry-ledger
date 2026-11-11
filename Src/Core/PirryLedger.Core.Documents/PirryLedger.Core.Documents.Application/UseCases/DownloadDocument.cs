using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Documents.Application.Exceptions;
using PirryLedger.Core.Documents.Application.Ports;

namespace PirryLedger.Core.Documents.Application.UseCases;

// RF-DOC-03: descargar un documento. Solo el duenio o un Administrator; a
// cualquier otro usuario se le rechaza (403, no 404: el documento existe y lo
// que falta es permiso).
//
// Un documento eliminado no se descarga: se comporta como inexistente.
public sealed class DownloadDocument
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentStore _store;
    private readonly Authenticate _authenticate;

    public DownloadDocument(
        IDocumentRepository repository,
        IDocumentStore store,
        Authenticate authenticate)
    {
        _repository = repository;
        _store = store;
        _authenticate = authenticate;
    }

    public async Task<DocumentContent> ExecuteAsync(
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

        var content = await _store.GetAsync(document.StorageKey, cancellationToken)
            ?? throw new DocumentNotFoundException();

        return new DocumentContent(content, document.Name, document.ContentType);
    }
}
