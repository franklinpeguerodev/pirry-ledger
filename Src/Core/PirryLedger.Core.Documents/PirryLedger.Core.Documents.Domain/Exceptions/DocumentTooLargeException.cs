namespace PirryLedger.Core.Documents.Domain.Exceptions;

// RF-DOC-01: el archivo supera el maximo permitido. Tiene tipo propio porque su
// codigo HTTP es 413 y no 400.
public sealed class DocumentTooLargeException : DocumentRuleViolationException
{
    public DocumentTooLargeException(long maxSizeBytes)
        : base($"El archivo supera el máximo permitido de {maxSizeBytes} bytes.")
    {
        MaxSizeBytes = maxSizeBytes;
    }

    public long MaxSizeBytes { get; }
}
