namespace PirryLedger.Core.Documents.Application.Ports;

// RF-DOC-01..04 (ADR 017): el almacenamiento de los bytes, detras de un puerto.
//
// La pieza no sabe si detras hay SeaweedFS, R2 o un disco: eso es
// Infrastructure. En las pruebas el puerto se sustituye por un doble en
// memoria, asi que los casos de uso corren sin red y sin servidor (RD-12).
//
// No hay borrado fisico: RF-DOC-04 pide que el registro siga existiendo en el
// almacenamiento, y el objeto solo se elimina cuando exista una limpieza
// decidida aparte.
public interface IDocumentStore
{
    Task PutAsync(
        string key,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    // Devuelve null si el objeto no existe. Un fallo de comunicacion no es
    // null: es DocumentStorageUnavailableException, para no confundir "no
    // esta" con "no se pudo preguntar".
    Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);
}
