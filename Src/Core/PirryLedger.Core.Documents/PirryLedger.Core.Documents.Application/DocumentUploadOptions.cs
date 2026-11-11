using PirryLedger.Core.Documents.Domain.Rules;

namespace PirryLedger.Core.Documents.Application;

// RF-DOC-01: el tope de una subida es una regla de la pieza con un valor por
// defecto, que el operador puede cambiar por configuracion. Vive en Application
// y no en Infrastructure: el caso de uso necesita el numero, no las
// credenciales del almacenamiento.
public sealed record DocumentUploadOptions(long MaxBytes)
{
    public static DocumentUploadOptions Default { get; } = new(DocumentRules.DefaultMaxSizeBytes);

    public long EffectiveMaxBytes => MaxBytes > 0 ? MaxBytes : DocumentRules.DefaultMaxSizeBytes;
}
