using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Documents.Application.Ports;
using PirryLedger.Core.Documents.Domain.Entities;
using PirryLedger.Core.Documents.Domain.Rules;

namespace PirryLedger.Core.Documents.Application.UseCases;

// RF-DOC-01: subir un documento. Registra nombre, tipo, tamano, fecha y quien
// lo subio, y guarda los bytes en el almacenamiento (ADR 017).
//
// El orden importa: las reglas del documento corren ANTES de tocar el
// almacenamiento, asi que un nombre invalido o un archivo vacio no llegan a la
// red; y el objeto se guarda ANTES de la fila, para que una fila vigente nunca
// apunte a un objeto que no existe.
public sealed class UploadDocument
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentStore _store;
    private readonly Authenticate _authenticate;
    private readonly IClock _clock;
    private readonly DocumentUploadOptions _options;

    public UploadDocument(
        IDocumentRepository repository,
        IDocumentStore store,
        Authenticate authenticate,
        IClock clock,
        DocumentUploadOptions options)
    {
        _repository = repository;
        _store = store;
        _authenticate = authenticate;
        _clock = clock;
        _options = options;
    }

    public async Task<DocumentView> ExecuteAsync(
        string plainToken,
        string fileName,
        string? contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var caller = await _authenticate.ExecuteAsync(plainToken, cancellationToken);

        // La clave la genera el servidor: un Guid nuevo por subida. El nombre
        // del usuario nunca se usa como clave (ADR 017, decision 4).
        var storageKey = $"documents/{Guid.NewGuid()}";

        var document = Document.Create(
            fileName,
            contentType,
            content.LongLength,
            caller.Id,
            // La sucursal sale de la sesion, nunca del cuerpo de la peticion.
            // Para un Administrator es la sucursal global "Todas".
            caller.BranchId,
            storageKey,
            _clock.UtcNow,
            _options.EffectiveMaxBytes);

        await _store.PutAsync(storageKey, content, document.ContentType, cancellationToken);
        await _repository.AddAsync(document, cancellationToken);

        return DocumentViewMapper.From(document);
    }
}
