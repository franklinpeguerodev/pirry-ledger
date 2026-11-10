using PirryLedger.Core.Documents.Application.Exceptions;
using PirryLedger.Core.Documents.Application.Ports;

namespace PirryLedger.Core.Documents.Infrastructure.Storage;

// El almacenamiento que se registra cuando falta la configuracion (ADR 017,
// decision 9). La aplicacion arranca y las operaciones de documentos responden
// 503 controlado; el resto del proyecto sigue funcionando.
internal sealed class UnavailableDocumentStore : IDocumentStore
{
    public Task PutAsync(
        string key,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default) =>
        throw new DocumentStorageNotConfiguredException();

    public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        throw new DocumentStorageNotConfiguredException();
}
