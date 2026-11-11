using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Documents.Application.Ports;

namespace PirryLedger.Core.Documents.Application.UseCases;

// RF-DOC-02: listar los documentos que el usuario tiene permitido ver.
//
// Un Standard ve solo los suyos; un Administrator ve todos. Los eliminados no
// aparecen para nadie: la eliminacion logica los saca del listado.
public sealed class ListDocuments
{
    private readonly IDocumentRepository _repository;
    private readonly Authenticate _authenticate;

    public ListDocuments(IDocumentRepository repository, Authenticate authenticate)
    {
        _repository = repository;
        _authenticate = authenticate;
    }

    public async Task<IReadOnlyList<DocumentView>> ExecuteAsync(
        string plainToken,
        CancellationToken cancellationToken = default)
    {
        var caller = await _authenticate.ExecuteAsync(plainToken, cancellationToken);

        var documents = caller.Role == Role.Administrator
            ? await _repository.ListAllAsync(cancellationToken)
            : await _repository.ListByOwnerAsync(caller.Id, cancellationToken);

        // El repositorio ya filtra, y aqui se vuelve a comprobar: la regla de
        // RF-DOC-02 se lee en el caso de uso y no depende de que un
        // repositorio concreto se acuerde de aplicarla.
        return documents
            .Where(document => !document.IsDeleted)
            .Select(DocumentViewMapper.From)
            .ToArray();
    }
}
