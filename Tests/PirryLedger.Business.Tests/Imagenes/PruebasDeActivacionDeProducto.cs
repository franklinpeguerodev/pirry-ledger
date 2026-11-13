using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Infrastructure.Persistence;

namespace PirryLedger.Business.Tests.Imagenes;

// Activar y desactivar un producto cambia si su foto forma parte del conjunto
// que el cliente descarga (ADR 015). La foto se queda en el bucket: al
// reactivar el producto la vuelve a necesitar.
public sealed class PruebasDeActivacionDeProducto
{
    [Fact]
    public async Task DesactivarUnProductoReservaLaRevisionYLoDejaInactivo()
    {
        // Arrange
        await using var contexto = await ContextoAsync();
        var producto = await ProductoAsync(contexto);
        var repositorio = new ProductRepository(contexto);

        // Act
        var revision = await repositorio.SetActiveAsync(producto, false);

        // Assert
        Assert.Equal(1, revision);
        Assert.False(producto.IsActive);
        Assert.Equal(1, producto.ImageRevision);
    }

    [Fact]
    public async Task ReactivarUnProductoReservaUnaRevisionNueva()
    {
        // Arrange
        await using var contexto = await ContextoAsync();
        var producto = await ProductoAsync(contexto);
        var repositorio = new ProductRepository(contexto);

        await repositorio.SetActiveAsync(producto, false);

        // Act
        var revision = await repositorio.SetActiveAsync(producto, true);

        // Assert
        Assert.Equal(2, revision);
        Assert.True(producto.IsActive);
        Assert.Equal(2, producto.ImageRevision);
    }

    [Fact]
    public async Task ElCasoDeUsoDesactivaElProductoYGuardaElCambio()
    {
        // Arrange
        await using var contexto = await ContextoAsync();
        var producto = await ProductoAsync(contexto);
        var repositorio = new ProductRepository(contexto);
        var caso = new SetProductActive(repositorio);

        // Act
        var resultado = await caso.ExecuteAsync(producto.Id, false);

        // Assert
        Assert.False(resultado.IsActive);
        Assert.Equal(1, resultado.ImageRevision);
    }

    private static async Task<BusinessDbContext> ContextoAsync()
    {
        var options = new DbContextOptionsBuilder<BusinessDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // El proveedor en memoria no tiene transacciones: avisa y sigue.
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var contexto = new BusinessDbContext(options);

        // El contador global de revisiones: una sola fila, como en la base.
        contexto.ImageRevisionCounters.Add(new ImageRevisionCounter { Id = 1, Value = 0 });

        await contexto.SaveChangesAsync();

        return contexto;
    }

    private static async Task<Product> ProductoAsync(BusinessDbContext contexto)
    {
        var producto = Product.Create("Combo Pirri", "Producto de prueba", 500m);
        contexto.Products.Add(producto);
        await contexto.SaveChangesAsync();

        return producto;
    }
}
