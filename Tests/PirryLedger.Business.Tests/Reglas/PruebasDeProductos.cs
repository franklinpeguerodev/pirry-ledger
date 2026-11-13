using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Tests.Reglas;

public sealed class PruebasDeProductos
{
    [Fact]
    public void CrearProductoActivoConDatosValidos()
    {
        var producto = Product.Create("Combo Pirri", "Hamburguesa y papas", 500m);

        Assert.NotEqual(Guid.Empty, producto.Id);
        Assert.True(producto.IsActive);
        Assert.Equal("Combo Pirri", producto.Name);
        Assert.Equal(500m, producto.Price);
    }

    [Fact]
    public void CrearProductoConPrecioInvalidoSeRechaza()
    {
        Assert.Throws<InvoiceRuleViolationException>(
            () => Product.Create("Combo Pirri", "Producto de prueba", 500.005m));
    }

    [Fact]
    public void DesactivarProductoNoCambiaSuPrecioHistorico()
    {
        var producto = Product.Create("Combo Pirri", "Producto de prueba", 500m);

        producto.SetActive(false);

        Assert.False(producto.IsActive);
        Assert.Equal(500m, producto.Price);
    }

    [Fact]
    public void CrearItemDesdeProductoConservaLaFotoDelNombreYPrecio()
    {
        var producto = Product.Create("Combo Pirri", "Producto de prueba", 500m);
        var item = InvoiceItem.Create(producto, 2);

        producto.Update("Combo Pirri grande", "Producto de prueba", 700m);

        Assert.Equal(producto.Id, item.ProductId);
        Assert.Equal("Combo Pirri", item.Name);
        Assert.Equal(500m, item.UnitPrice);
        Assert.Equal(1000m, item.LineTotal);
    }
}
