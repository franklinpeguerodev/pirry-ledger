namespace PirryLedger.Business.Application.Exceptions;

public sealed class ProductInactiveException : Exception
{
    public ProductInactiveException() : base("El producto indicado no esta activo.")
    {
    }
}
