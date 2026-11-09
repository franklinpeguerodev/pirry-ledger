namespace PirryLedger.Business.Application.Exceptions;

// El archivo recibido no es una imagen admitida, o esta roto de una forma que
// no se puede leer. El endpoint lo traduce a 415.
public sealed class UnsupportedProductImageException : Exception
{
    public UnsupportedProductImageException()
        : base("El archivo no es una imagen JPEG, PNG ni WebP valida.")
    {
    }

    public UnsupportedProductImageException(string message)
        : base(message)
    {
    }
}
