namespace PirryLedger.Core.Documents.Domain.Exceptions;

// Una regla del documento no se cumple. Es el rechazo controlado de RD-07: el
// caso de uso la deja subir y el endpoint la convierte en 400, nunca en una
// traza. Las que tienen un codigo distinto (archivo vacio, archivo demasiado
// grande) heredan de esta.
public class DocumentRuleViolationException : Exception
{
    public DocumentRuleViolationException(string message) : base(message)
    {
    }
}
