using PirryLedger.Core.Documents.Domain.Rules;

namespace PirryLedger.Core.Documents.Infrastructure.Storage;

// La configuracion del almacenamiento de documentos (ADR 017, decision 11).
//
// Todas las variables son opcionales al arrancar: si falta una, la aplicacion
// levanta y las operaciones de documentos responden 503 controlado. Es lo que
// pide la seccion 1.4 del enunciado ("el resto del proyecto sigue
// funcionando"), y es lo contrario del patron del ADR 015 para las imagenes.
public sealed class DocumentStorageConfiguration
{
    public DocumentStorageConfiguration(
        string endpoint,
        string accessKeyId,
        string secretAccessKey,
        string bucket,
        long maxBytes)
    {
        Endpoint = endpoint.Trim();
        AccessKeyId = accessKeyId.Trim();
        SecretAccessKey = secretAccessKey.Trim();
        Bucket = bucket.Trim();
        MaxBytes = maxBytes;
    }

    // Endpoint S3 de SeaweedFS dentro de la composicion (por ejemplo
    // http://seaweedfs:8333). Nunca se imprime (RD-08).
    public string Endpoint { get; }

    public string AccessKeyId { get; }

    public string SecretAccessKey { get; }

    public string Bucket { get; }

    // Tope de una subida. El valor por defecto vive en DocumentRules.
    public long MaxBytes { get; }

    // La configuracion por defecto, para cuando faltan variables: el limite de
    // tamano sigue siendo el del sistema aunque no haya almacenamiento.
    public static DocumentStorageConfiguration Empty { get; } =
        new(string.Empty, string.Empty, string.Empty, string.Empty, DocumentRules.DefaultMaxSizeBytes);
}
