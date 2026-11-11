namespace PirryLedger.Core.Documents.Domain.Exceptions;

// RF-DOC-01 + RD-07: "un archivo ausente o vacio produce un rechazo controlado".
// Tiene su propio tipo porque el codigo HTTP es 400 con un mensaje distinto al
// de una regla cualquiera.
public sealed class EmptyDocumentException : DocumentRuleViolationException
{
    public EmptyDocumentException()
        : base("El archivo está vacío.")
    {
    }
}
