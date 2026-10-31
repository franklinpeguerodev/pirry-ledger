namespace PirryLedger.Business.Application.Exceptions;

// RD-07: la factura pedida no existe. El endpoint la traduce a 404 con el mismo
// formato de error que el resto del API.
public sealed class InvoiceNotFoundException : Exception
{
    public InvoiceNotFoundException() : base("La factura indicada no existe.")
    {
    }
}
