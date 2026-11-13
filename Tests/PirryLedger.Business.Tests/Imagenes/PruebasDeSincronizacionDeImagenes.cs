using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PirryLedger.Business.Application;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Infrastructure.Persistence;
using PirryLedger.Business.Tests.Operaciones;

namespace PirryLedger.Business.Tests.Imagenes;

// La sincronizacion de las imagenes (ADR 015). La prueba usa el repositorio
// de verdad sobre EF InMemory: el delta se calcula con el historial real, sin
// base de datos ni bucket (RD-12).
public sealed class PruebasDeSincronizacionDeImagenes
{
    private static readonly DateTime Ahora = new(2026, 11, 9, 18, 0, 0, DateTimeKind.Utc);

    private static readonly ProductImageOptions Opciones = new(5_242_880, 15);

    [Fact]
    public async Task LaSincronizacionCompletaTraeLasImagenesDeLosProductosActivos()
    {
        await using var contexto = await ContextoAsync();
        var producto = await ProductoAsync(contexto, activo: true);
        var repositorio = new ProductImageRepository(contexto);
        var imagen = await SubirAsync(repositorio, producto);

        var store = new FakeImageStore();
        var caso = new SyncProductImages(repositorio, store, new FakeClock(Ahora), Opciones);

        var resultado = await caso.ExecuteAsync(null);

        Assert.True(resultado.FullSync);
        Assert.Equal(1, resultado.Revision);
        var alta = Assert.Single(resultado.Upserts);
        Assert.Equal(imagen.ImageKey, alta.ImageKey);
        Assert.Equal(imagen.Version, alta.Version);
        Assert.Empty(resultado.Removed);
    }

    [Fact]
    public async Task LaSincronizacionCompletaNoTraeLasImagenesDeLosProductosInactivos()
    {
        await using var contexto = await ContextoAsync();
        var producto = await ProductoAsync(contexto, activo: false);
        var repositorio = new ProductImageRepository(contexto);
        await SubirAsync(repositorio, producto);

        var caso = new SyncProductImages(
            repositorio,
            new FakeImageStore(),
            new FakeClock(Ahora),
            Opciones);

        var resultado = await caso.ExecuteAsync(null);

        Assert.Empty(resultado.Upserts);
    }

    [Fact]
    public async Task LaSincronizacionDevuelveSoloLoQueCambioDespuesDelCursor()
    {
        await using var contexto = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);

        var primero = await ProductoAsync(contexto, activo: true);
        await SubirAsync(repositorio, primero);

        var segundo = await ProductoAsync(contexto, activo: true, nombre: "Refresco");
        var segunda = await SubirAsync(repositorio, segundo);

        var caso = new SyncProductImages(
            repositorio,
            new FakeImageStore(),
            new FakeClock(Ahora),
            Opciones);

        var resultado = await caso.ExecuteAsync(1);

        var alta = Assert.Single(resultado.Upserts);
        Assert.Equal(segunda.ImageKey, alta.ImageKey);
        Assert.Empty(resultado.Removed);
    }

    [Fact]
    public async Task LaSincronizacionDevuelveLasImagenesQueYaNoExisten()
    {
        await using var contexto = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);
        var producto = await ProductoAsync(contexto, activo: true);
        var imagen = await SubirAsync(repositorio, producto);

        await repositorio.RetireLiveImageAsync(producto.Id, Ahora);

        var caso = new SyncProductImages(
            repositorio,
            new FakeImageStore(),
            new FakeClock(Ahora),
            Opciones);

        var resultado = await caso.ExecuteAsync(1);

        Assert.Equal(imagen.ImageKey, Assert.Single(resultado.Removed));
        Assert.Empty(resultado.Upserts);
    }

    [Fact]
    public async Task LaSincronizacionTraeLaImagenDeUnProductoQueSeReactivo()
    {
        await using var contexto = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);
        var producto = await ProductoAsync(contexto, activo: true);
        var imagen = await SubirAsync(repositorio, producto);

        var productos = new ProductRepository(contexto);
        await productos.SetActiveAsync(producto, false);
        await productos.SetActiveAsync(producto, true);

        var caso = new SyncProductImages(
            repositorio,
            new FakeImageStore(),
            new FakeClock(Ahora),
            Opciones);

        var resultado = await caso.ExecuteAsync(1);

        var alta = Assert.Single(resultado.Upserts);
        Assert.Equal(imagen.ImageKey, alta.ImageKey);
        Assert.Empty(resultado.Removed);
    }

    [Fact]
    public async Task UnCursorDelFuturoSeRechaza()
    {
        await using var contexto = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);
        var producto = await ProductoAsync(contexto, activo: true);
        await SubirAsync(repositorio, producto);

        var caso = new SyncProductImages(
            repositorio,
            new FakeImageStore(),
            new FakeClock(Ahora),
            Opciones);

        await Assert.ThrowsAsync<InvalidProductImageCursorException>(
            () => caso.ExecuteAsync(99));
    }

    [Fact]
    public async Task SoloSeFirmanLasImagenesQueElClienteNoTiene()
    {
        await using var contexto = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);

        var primero = await ProductoAsync(contexto, activo: true);
        await SubirAsync(repositorio, primero);

        var segundo = await ProductoAsync(contexto, activo: true, nombre: "Refresco");
        var segunda = await SubirAsync(repositorio, segundo);

        var store = new FakeImageStore();
        var caso = new SyncProductImages(repositorio, store, new FakeClock(Ahora), Opciones);

        // El cliente ya tiene la primera imagen: solo se firma la segunda.
        var resultado = await caso.ExecuteAsync(1);

        Assert.Equal(segunda.ImageKey, Assert.Single(resultado.Upserts).ImageKey);

        var firmada = Assert.Single(store.Signed);
        Assert.Equal(segunda.ImageKey, firmada.Key);
        Assert.Equal("image/jpeg", firmada.ContentType);
        Assert.Equal(Ahora.AddMinutes(15), firmada.ExpiresAtUtc);
    }

    [Fact]
    public async Task LaFirmaVenceEnElTiempoConfigurado()
    {
        await using var contexto = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);
        var producto = await ProductoAsync(contexto, activo: true);
        await SubirAsync(repositorio, producto);

        var caso = new SyncProductImages(
            repositorio,
            new FakeImageStore(),
            new FakeClock(Ahora),
            Opciones);

        var resultado = await caso.ExecuteAsync(null);

        Assert.Equal(Ahora.AddMinutes(15), Assert.Single(resultado.Upserts).ExpiresAtUtc);
    }

    private static async Task<ProductImage> SubirAsync(
        ProductImageRepository repositorio,
        Product producto)
    {
        var imagen = ProductImage.Create(
            producto.Id,
            System.Text.Encoding.UTF8.GetBytes("abc"),
            ImageFormat.Jpeg,
            Ahora);

        await repositorio.SetLiveImageAsync(imagen, Ahora);

        return imagen;
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

    private static async Task<Product> ProductoAsync(
        BusinessDbContext contexto,
        bool activo,
        string nombre = "Combo Pirri")
    {
        var producto = Product.Create(nombre, "Producto de prueba", 500m);

        if (!activo)
        {
            producto.SetActive(false);
        }

        contexto.Products.Add(producto);
        await contexto.SaveChangesAsync();

        return producto;
    }
}
