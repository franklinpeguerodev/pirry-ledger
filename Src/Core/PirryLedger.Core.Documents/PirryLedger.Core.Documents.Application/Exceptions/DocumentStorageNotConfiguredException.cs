namespace PirryLedger.Core.Documents.Application.Exceptions;

// Seccion 1.4 del enunciado: sin la clave, la operacion devuelve un rechazo
// controlado y el resto del proyecto sigue funcionando. Por eso la aplicacion
// arranca igual y esta excepcion es la respuesta de las operaciones de
// documentos, no un error de arranque (ADR 017, decision 9).
public sealed class DocumentStorageNotConfiguredException : Exception
{
    public DocumentStorageNotConfiguredException()
        : base("El almacenamiento de documentos no está configurado. Revisa el README.")
    {
    }
}
