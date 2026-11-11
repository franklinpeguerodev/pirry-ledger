namespace PirryLedger.Core.Documents.Infrastructure.Storage;

// La configuracion del almacenamiento de documentos (ADR 017, decision 11).
//
// Todas las variables son opcionales al arrancar: si falta una, la aplicacion
// levanta y las operaciones de documentos responden 503 controlado. Es lo que
// pide la seccion 1.4 del enunciado ("el resto del proyecto sigue
// funcionando"), y es lo contrario del patron del ADR 015 para las imagenes.
//
// Los valores nunca se imprimen (RD-08): ni el endpoint, ni el bucket, ni las
// claves.
public sealed class DocumentStorageConfiguration
{
    public DocumentStorageConfiguration(
        string endpoint,
        string accessKeyId,
        string secretAccessKey,
        string bucket)
    {
        Endpoint = endpoint.Trim();
        AccessKeyId = accessKeyId.Trim();
        SecretAccessKey = secretAccessKey.Trim();
        Bucket = bucket.Trim();
    }

    // Endpoint S3 de SeaweedFS dentro de la composicion (por ejemplo
    // http://seaweedfs:8333).
    public string Endpoint { get; }

    public string AccessKeyId { get; }

    public string SecretAccessKey { get; }

    public string Bucket { get; }

    public bool IsConfigured =>
        Endpoint.Length > 0
        && AccessKeyId.Length > 0
        && SecretAccessKey.Length > 0
        && Bucket.Length > 0;

    public static DocumentStorageConfiguration Empty { get; } =
        new(string.Empty, string.Empty, string.Empty, string.Empty);
}
