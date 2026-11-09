namespace PirryLedger.Business.Application.Ports;

// El almacenamiento de los archivos de imagen (ADR 015).
//
// El modulo de negocio no sabe que hay un bucket: solo pide guardar un
// archivo, borrar uno y firmar su descarga. El puerto vive en la capa de
// Application y la implementacion, que habla con Cloudflare R2, en la de
// Infrastructure.
public interface IImageStore
{
    // Guarda el contenido con la clave indicada y lo registra con el tipo de
    // contenido que el servidor valido.
    Task PutAsync(
        string key,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    // Borrar es idempotente: si el objeto no existe, es un exito.
    //
    // Devuelve false cuando el almacenamiento no responde. El llamador lo
    // usa para dejar constancia: el objeto se queda en el bucket como imagen
    // residual, que se asume por ahora (ADR 015).
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

    // La firma caduca en expiresAtUtc. El tipo de contenido viaja dentro de
    // la firma para que el bucket sirva el archivo con el tipo que el servidor
    // valido, y no con el que dijo quien lo subio.
    //
    // Firmar es un calculo local: no hay llamada de red.
    string CreateDownloadUrl(string key, string contentType, DateTime expiresAtUtc);
}
