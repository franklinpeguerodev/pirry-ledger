namespace PirryLedger.Core.Documents.Domain.Exceptions;

// RF-DOC-04: la eliminacion es un cambio de estado, no un borrado. Volver a
// eliminarlo es un rechazo controlado, no un segundo cambio de fecha.
public sealed class DocumentAlreadyDeletedException : DocumentRuleViolationException
{
    public DocumentAlreadyDeletedException()
        : base("El documento ya fue eliminado.")
    {
    }
}
