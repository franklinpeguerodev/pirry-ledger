namespace PirryLedger.Business.Application.Exceptions;

// El cursor de la sincronizacion es del futuro: el servidor no lo emitio, asi
// que no se puede saber que cambios le faltan. El endpoint lo traduce a 400.
public sealed class InvalidProductImageCursorException : Exception
{
    public InvalidProductImageCursorException(long since, long current)
        : base($"El cursor de la sincronizacion ({since}) es posterior a la revision actual ({current}).")
    {
        Since = since;
        Current = current;
    }

    public long Since { get; }

    public long Current { get; }
}
