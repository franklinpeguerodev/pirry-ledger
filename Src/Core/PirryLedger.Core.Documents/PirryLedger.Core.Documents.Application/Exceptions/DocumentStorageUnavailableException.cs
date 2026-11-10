namespace PirryLedger.Core.Documents.Application.Exceptions;

// RD-07 y RD-08: el almacenamiento no responde. El endpoint lo convierte en
// 503 con un mensaje comprensible, sin trazas ni el mensaje del SDK (que puede
// traer el bucket o el endpoint).
public sealed class DocumentStorageUnavailableException : Exception
{
    public DocumentStorageUnavailableException()
        : base("El almacenamiento de documentos no está disponible en este momento.")
    {
    }
}
