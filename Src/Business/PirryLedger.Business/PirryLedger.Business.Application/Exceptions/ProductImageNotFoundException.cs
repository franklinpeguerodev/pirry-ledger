namespace PirryLedger.Business.Application.Exceptions;

// El producto no tiene ninguna imagen vigente. El endpoint lo traduce a 404.
public sealed class ProductImageNotFoundException : Exception
{
    public ProductImageNotFoundException()
        : base("El producto indicado no tiene imagen.")
    {
    }
}
