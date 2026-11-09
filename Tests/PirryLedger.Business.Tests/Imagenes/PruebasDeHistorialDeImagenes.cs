using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Infrastructure.Persistence;

namespace PirryLedger.Business.Tests.Imagenes;

// El historial del conjunto de imagenes (ADR 015). La prueba usa EF InMemory:
// sin base real (RD-12). El bloqueo del contador es de PostgreSQL y se
// comprueba aparte contra la base de desarrollo.
public sealed class PruebasDeHistorialDeImagenes
{
    private static readonly DateTime Ahora = new(2026, 11, 9, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UnaImagenGuardadaQuedaVigenteConSuRevision()
    {
        var (contexto, producto) = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);

        var imagen = ProductImage.Create(producto.Id, Contenido(), ImageFormat.Jpeg, Ahora);
        var cambio = await repositorio.SetLiveImageAsync(imagen, Ahora);

        Assert.Equal(1, cambio.Revision);
        Assert.Null(cambio.RetiredImageKey);
        Assert.Equal(1, await repositorio.CurrentRevisionAsync());

        var vigente = await repositorio.FindLiveAsync(producto.Id);
        Assert.NotNull(vigente);
        Assert.Equal(1, vigente!.RevisionAdded);
        Assert.Null(vigente.RevisionRemoved);
        Assert.True(vigente.IsLive);
    }

    [Fact]
    public async Task SustituirLaImagenRetiraLaAnteriorYDejaLaNuevaVigente()
    {
        var (contexto, producto) = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);

        var primera = ProductImage.Create(producto.Id, Contenido(), ImageFormat.Jpeg, Ahora);
        await repositorio.SetLiveImageAsync(primera, Ahora);

        var segunda = ProductImage.Create(producto.Id, Contenido("otra"), ImageFormat.Jpeg, Ahora);
        var cambio = await repositorio.SetLiveImageAsync(segunda, Ahora);

        Assert.Equal(2, cambio.Revision);
        Assert.Equal(primera.ImageKey, cambio.RetiredImageKey);

        var vigente = await repositorio.FindLiveAsync(producto.Id);
        Assert.NotNull(vigente);
        Assert.Equal(segunda.ImageKey, vigente!.ImageKey);
        Assert.Equal(2, vigente.RevisionAdded);
    }

    [Fact]
    public async Task RetirarLaImagenLaSacaDelConjunto()
    {
        var (contexto, producto) = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);

        var imagen = ProductImage.Create(producto.Id, Contenido(), ImageFormat.Jpeg, Ahora);
        await repositorio.SetLiveImageAsync(imagen, Ahora);

        var cambio = await repositorio.RetireLiveImageAsync(producto.Id, Ahora);

        Assert.Equal(imagen.ImageKey, cambio.RetiredImageKey);
        Assert.Null(await repositorio.FindLiveAsync(producto.Id));
        Assert.Empty(await repositorio.ListLiveAsync());
    }

    [Fact]
    public async Task RetirarUnProductoSinImagenNoCambiaNada()
    {
        var (contexto, producto) = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);

        var cambio = await repositorio.RetireLiveImageAsync(producto.Id, Ahora);

        Assert.Null(cambio.RetiredImageKey);
        Assert.Null(await repositorio.FindLiveAsync(producto.Id));
    }

    [Fact]
    public async Task LasAltasTraenLoQueElClienteNoTiene()
    {
        var (contexto, producto) = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);

        var imagen = ProductImage.Create(producto.Id, Contenido(), ImageFormat.Jpeg, Ahora);
        await repositorio.SetLiveImageAsync(imagen, Ahora);

        var altas = await repositorio.ListUpsertsSinceAsync(0);

        Assert.Single(altas);
        Assert.Equal(imagen.ImageKey, altas[0].ImageKey);
        Assert.Empty(await repositorio.ListUpsertsSinceAsync(1));
    }

    [Fact]
    public async Task LasBajasTraenLaImagenQueSeQuito()
    {
        var (contexto, producto) = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);

        var imagen = ProductImage.Create(producto.Id, Contenido(), ImageFormat.Jpeg, Ahora);
        await repositorio.SetLiveImageAsync(imagen, Ahora);
        await repositorio.RetireLiveImageAsync(producto.Id, Ahora);

        var bajas = await repositorio.ListRemovedKeysSinceAsync(1);

        Assert.Equal(imagen.ImageKey, Assert.Single(bajas));
    }

    [Fact]
    public async Task LasBajasTraenLaImagenDelProductoDesactivado()
    {
        var (contexto, producto) = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);

        var imagen = ProductImage.Create(producto.Id, Contenido(), ImageFormat.Jpeg, Ahora);
        await repositorio.SetLiveImageAsync(imagen, Ahora);

        producto.SetActive(false);
        producto.MarkImageSetChanged(2);
        await contexto.SaveChangesAsync();

        var bajas = await repositorio.ListRemovedKeysSinceAsync(1);

        Assert.Equal(imagen.ImageKey, Assert.Single(bajas));
        Assert.Empty(await repositorio.ListUpsertsSinceAsync(1));
    }

    [Fact]
    public async Task LasAltasTraenLaImagenCuandoElProductoSeReactiva()
    {
        var (contexto, producto) = await ContextoAsync();
        var repositorio = new ProductImageRepository(contexto);

        var imagen = ProductImage.Create(producto.Id, Contenido(), ImageFormat.Jpeg, Ahora);
        await repositorio.SetLiveImageAsync(imagen, Ahora);

        producto.SetActive(false);
        producto.MarkImageSetChanged(2);
        await contexto.SaveChangesAsync();

        producto.SetActive(true);
        producto.MarkImageSetChanged(3);
        await contexto.SaveChangesAsync();

        var altas = await repositorio.ListUpsertsSinceAsync(1);

        Assert.Equal(imagen.ImageKey, Assert.Single(altas).ImageKey);
        Assert.Empty(await repositorio.ListRemovedKeysSinceAsync(1));
    }

    private static byte[] Contenido(string texto = "abc") =>
        System.Text.Encoding.UTF8.GetBytes(texto);

    private static DbContextOptions<BusinessDbContext> OpcionesEnMemoria() =>
        new DbContextOptionsBuilder<BusinessDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // El proveedor en memoria no tiene transacciones: avisa y sigue.
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static async Task<(BusinessDbContext Contexto, Product Producto)> ContextoAsync()
    {
        var contexto = new BusinessDbContext(OpcionesEnMemoria());

        // El contador global de revisiones: una sola fila, como en la base.
        contexto.ImageRevisionCounters.Add(new ImageRevisionCounter { Id = 1, Value = 0 });

        var producto = Product.Create("Combo Pirri", null, 500m);
        contexto.Products.Add(producto);

        await contexto.SaveChangesAsync();

        return (contexto, producto);
    }
}
