namespace PirryLedger.Business.Application.Exceptions;

// El almacenamiento de imagenes no responde. El endpoint lo traduce a 503 con
// un mensaje generico: el error del bucket puede traer el nombre del bucket o
// el endpoint, y eso no se le enseña al cliente (RD-08).
public sealed class ImageStorageUnavailableException : Exception
{
    public ImageStorageUnavailableException()
        : base("El almacenamiento de imagenes no esta disponible.")
    {
    }
}
