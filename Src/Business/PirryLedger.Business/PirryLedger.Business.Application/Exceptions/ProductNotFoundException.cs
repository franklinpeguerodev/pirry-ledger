namespace PirryLedger.Business.Application.Exceptions;

public sealed class ProductNotFoundException : Exception
{
    public ProductNotFoundException() : base("El producto indicado no existe.")
    {
    }
}
