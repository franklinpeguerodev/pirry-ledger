namespace PirryLedger.Core.Documents.Application.Exceptions;

// RF-DOC-03 y RF-DOC-04: el documento existe, pero quien llama no es su duenio
// ni un Administrator. Se responde 403 y no se revela nada del documento.
public sealed class DocumentAccessDeniedException : Exception
{
    public DocumentAccessDeniedException()
        : base("No tiene permiso para operar sobre este documento.")
    {
    }
}
