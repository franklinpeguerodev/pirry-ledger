using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Tests.Operaciones;

namespace PirryLedger.Business.Tests.Reglas;

public sealed class PruebasDeAlcancePorSucursal
{
    [Fact]
    public async Task UnEstandarNoPuedeLeerUnaFacturaDeOtraSucursal()
    {
        var repository = new FakeInvoiceRepository();
        var ownBranch = Guid.NewGuid();
        var otherBranch = Guid.NewGuid();
        var ownInvoice = CreateInvoice(ownBranch);
        var otherInvoice = CreateInvoice(otherBranch);
        repository.Seed(ownInvoice);
        repository.Seed(otherInvoice);

        var visible = await repository.ListAsync(ownBranch);

        Assert.Single(visible);
        Assert.Equal(ownInvoice.Id, visible[0].Id);
    }

    [Fact]
    public async Task ElAdministradorPuedeLeerFacturasDeTodasLasSucursales()
    {
        var repository = new FakeInvoiceRepository();
        var first = CreateInvoice(Guid.NewGuid());
        var second = CreateInvoice(Guid.NewGuid());
        repository.Seed(first);
        repository.Seed(second);

        var visible = await repository.ListAsync();

        Assert.Equal(2, visible.Count);
    }

    private static Invoice CreateInvoice(Guid branchId)
    {
        var invoice = Invoice.Create();
        invoice.Issue(
            [InvoiceItem.Create("Producto", 10m, 1)],
            isDelivery: false,
            DateTime.UtcNow,
            branchId,
            "Sucursal",
            "Direccion");
        return invoice;
    }
}
