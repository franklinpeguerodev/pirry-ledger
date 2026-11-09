using PirryLedger.Business.Application;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Tests.Operaciones;

namespace PirryLedger.Business.Tests.Imagenes;

// Subir y quitar la imagen de un producto (ADR 015).
public sealed class PruebasDeSubidaDeImagen
{
    private static readonly DateTime Instante = new(2026, 11, 9, 17, 0, 0, DateTimeKind.Utc);

    private static readonly ProductImageOptions Opciones = new(5_242_880, 15);

    [Fact]
    public async Task SubirLaImagenSubeElArchivoYDevuelveLaClave()
    {
        var preparado = Preparar();

        var imagen = await preparado.Caso.ExecuteAsync(preparado.Producto.Id, Jpeg(), CancellationToken.None);

        var subida = Assert.Single(preparado.Store.Uploads);
        Assert.Equal(imagen.ImageKey, subida.Key);
        Assert.Equal("image/jpeg", subida.ContentType);
        Assert.Equal(imagen.ImageKey, (await preparado.Imagenes.FindLiveAsync(preparado.Producto.Id))!.ImageKey);
        Assert.Empty(preparado.Store.Deleted);
    }

    [Fact]
    public async Task LaImagenSeGuardaSinSusMetadatos()
    {
        var preparado = Preparar();

        await preparado.Caso.ExecuteAsync(preparado.Producto.Id, JpegConExif(), CancellationToken.None);

        var subida = Assert.Single(preparado.Store.Uploads);
        Assert.DoesNotContain("GPS", System.Text.Encoding.ASCII.GetString(subida.Content));
    }

    [Fact]
    public async Task UnaImagenDemasiadoGrandeSeRechazaYNoSeSube()
    {
        var preparado = Preparar();

        await Assert.ThrowsAsync<ProductImageTooLargeException>(
            () => preparado.Caso.ExecuteAsync(preparado.Producto.Id, new byte[5_242_881], CancellationToken.None));

        Assert.Empty(preparado.Store.Uploads);
    }

    [Fact]
    public async Task UnArchivoQueNoEsImagenSeRechazaYNoSeSube()
    {
        var preparado = Preparar();

        await Assert.ThrowsAsync<UnsupportedProductImageException>(
            () => preparado.Caso.ExecuteAsync(preparado.Producto.Id, System.Text.Encoding.UTF8.GetBytes("esto es un texto"), CancellationToken.None));

        Assert.Empty(preparado.Store.Uploads);
    }

    [Fact]
    public async Task SubirLaMismaImagenNoCambiaNada()
    {
        var preparado = Preparar();
        var contenido = Jpeg();

        var primera = await preparado.Caso.ExecuteAsync(preparado.Producto.Id, contenido, CancellationToken.None);
        var segunda = await preparado.Caso.ExecuteAsync(preparado.Producto.Id, contenido, CancellationToken.None);

        Assert.Equal(primera.ImageKey, segunda.ImageKey);
        Assert.Single(preparado.Store.Uploads);
        Assert.Empty(preparado.Store.Deleted);
    }

    [Fact]
    public async Task ReemplazarLaImagenBorraElArchivoAnterior()
    {
        var preparado = Preparar();

        var primera = await preparado.Caso.ExecuteAsync(preparado.Producto.Id, Jpeg(), CancellationToken.None);
        await preparado.Caso.ExecuteAsync(preparado.Producto.Id, JpegConOtroContenido(), CancellationToken.None);

        Assert.Equal(2, preparado.Store.Uploads.Count);
        Assert.Equal(primera.ImageKey, Assert.Single(preparado.Store.Deleted));
    }

    [Fact]
    public async Task SiLaBaseFallaSeBorraElArchivoNuevo()
    {
        var preparado = Preparar();
        preparado.Imagenes.FailOnSet = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => preparado.Caso.ExecuteAsync(preparado.Producto.Id, Jpeg(), CancellationToken.None));

        var subida = Assert.Single(preparado.Store.Uploads);
        Assert.Equal(subida.Key, Assert.Single(preparado.Store.Deleted));
    }

    [Fact]
    public async Task SiElBucketNoRespondeLaSubidaSeRechaza()
    {
        var preparado = Preparar();
        preparado.Store.FailOnPut = true;

        await Assert.ThrowsAsync<ImageStorageUnavailableException>(
            () => preparado.Caso.ExecuteAsync(preparado.Producto.Id, Jpeg(), CancellationToken.None));
    }

    [Fact]
    public async Task SubirLaImagenDeUnProductoInactivoSeRechaza()
    {
        var preparado = Preparar();
        preparado.Producto.SetActive(false);

        await Assert.ThrowsAsync<ProductInactiveException>(
            () => preparado.Caso.ExecuteAsync(preparado.Producto.Id, Jpeg(), CancellationToken.None));

        Assert.Empty(preparado.Store.Uploads);
    }

    [Fact]
    public async Task SubirLaImagenDeUnProductoQueNoExisteSeRechaza()
    {
        var preparado = Preparar();

        await Assert.ThrowsAsync<ProductNotFoundException>(
            () => preparado.Caso.ExecuteAsync(Guid.NewGuid(), Jpeg(), CancellationToken.None));

        Assert.Empty(preparado.Store.Uploads);
    }

    [Fact]
    public async Task QuitarLaImagenLaSacaDelConjuntoYBorraElArchivo()
    {
        var preparado = Preparar();
        var subida = await preparado.Caso.ExecuteAsync(preparado.Producto.Id, Jpeg(), CancellationToken.None);

        await preparado.Quitar().ExecuteAsync(preparado.Producto.Id, CancellationToken.None);

        Assert.Null(await preparado.Imagenes.FindLiveAsync(preparado.Producto.Id));
        Assert.Equal(subida.ImageKey, Assert.Single(preparado.Store.Deleted));
    }

    [Fact]
    public async Task QuitarLaImagenDeUnProductoSinImagenSeRechaza()
    {
        var preparado = Preparar();

        await Assert.ThrowsAsync<ProductImageNotFoundException>(
            () => preparado.Quitar().ExecuteAsync(preparado.Producto.Id, CancellationToken.None));
    }

    [Fact]
    public async Task QuitarLaImagenDeUnProductoQueNoExisteSeRechaza()
    {
        var preparado = Preparar();

        await Assert.ThrowsAsync<ProductNotFoundException>(
            () => preparado.Quitar().ExecuteAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private static Preparado Preparar()
    {
        var producto = Product.Create("Combo Pirri", null, 500m);
        var productos = new FakeProductRepository();
        productos.Seed(producto);

        var imagenes = new FakeProductImageRepository();
        var store = new FakeImageStore();
        var caso = new SetProductImage(
            productos,
            imagenes,
            store,
            new FakeClock(Instante),
            Opciones);

        return new Preparado(productos, producto, imagenes, store, caso);
    }

    private sealed class Preparado(
        FakeProductRepository productos,
        Product producto,
        FakeProductImageRepository imagenes,
        FakeImageStore store,
        SetProductImage caso)
    {
        public FakeProductRepository Productos { get; } = productos;

        public Product Producto { get; } = producto;

        public FakeProductImageRepository Imagenes { get; } = imagenes;

        public FakeImageStore Store { get; } = store;

        public SetProductImage Caso { get; } = caso;

        public RemoveProductImage Quitar() =>
            new(Productos, Imagenes, Store, new FakeClock(Instante));
    }

    private static byte[] Jpeg() => JpegConPayload(null);

    private static byte[] JpegConExif() => JpegConPayload("GPSLatitude 18N30");

    // Otra foto de verdad: los datos de la imagen son distintos, no solo los
    // metadatos. Dos JPEG que solo difieren en metadatos son la MISMA
    // imagen, porque el hash se calcula sobre el contenido ya limpio.
    private static byte[] JpegConOtroContenido() => JpegConPayload(null, [0x55, 0x66, 0x77, 0x88]);

    // Un JPEG minimo pero valido: SOI, JFIF, DQT, SOS, datos y EOI. El
    // contenido del APP1 es el que se elimina como metadato.
    private static byte[] JpegConPayload(string? exif, byte[]? datos = null)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange(Segment(0xE0, [.. "JFIF\0"u8, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]));

        if (exif is not null)
        {
            bytes.AddRange(Segment(0xE1, [.. "Exif\0\0"u8, .. System.Text.Encoding.ASCII.GetBytes(exif)]));
        }

        bytes.AddRange(Segment(0xDB, [0x00, 0x01, 0x02, 0x03]));
        bytes.AddRange(Segment(0xDA, [0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]));
        bytes.AddRange(datos ?? [0x11, 0x22, 0x33, 0x44]);
        bytes.AddRange([0xFF, 0xD9]);

        return [.. bytes];
    }

    private static byte[] Segment(byte marker, byte[] payload)
    {
        var length = payload.Length + 2;
        var result = new List<byte> { 0xFF, marker, (byte)(length >> 8), (byte)(length & 0xFF) };
        result.AddRange(payload);
        return [.. result];
    }
}
