using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Tests.Imagenes;

// Reglas de la imagen de un producto (ADR 015). El contenido de estas pruebas
// es sintetico: al dominio solo le importa el formato que se le pasa y los
// bytes que se hashean, no que el archivo sea una imagen de verdad.
public sealed class PruebasDeImagenDeProducto
{
    private static readonly DateTime Instante = new(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CrearImagenGuardaLaClaveConElProductoYElIdentificador()
    {
        var producto = Guid.NewGuid();

        var imagen = ProductImage.Create(producto, Contenido("abc"), ImageFormat.Jpeg, Instante);

        Assert.Equal($"products/{producto:D}/{imagen.Id:D}.jpg", imagen.ImageKey);
        Assert.Equal(producto, imagen.ProductId);
        Assert.Equal("image/jpeg", imagen.ContentType);
        Assert.Equal(3, imagen.SizeBytes);
    }

    [Fact]
    public void LaVersionEsElSha256DelContenido()
    {
        // El sha256 de "abc". No se usa el metodo de la entidad para calcularlo:
        // asi la prueba comprueba el valor real y no la implementacion.
        const string esperado = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        var imagen = ProductImage.Create(Guid.NewGuid(), Contenido("abc"), ImageFormat.Jpeg, Instante);

        Assert.Equal(esperado, imagen.Version);
        Assert.Equal(64, imagen.Version.Length);
    }

    [Fact]
    public void DosImagenesConElMismoContenidoTienenClavesDistintas()
    {
        var producto = Guid.NewGuid();

        var primera = ProductImage.Create(producto, Contenido("abc"), ImageFormat.Jpeg, Instante);
        var segunda = ProductImage.Create(producto, Contenido("abc"), ImageFormat.Jpeg, Instante);

        Assert.NotEqual(primera.ImageKey, segunda.ImageKey);
        Assert.Equal(primera.Version, segunda.Version);
    }

    [Fact]
    public void UnaImagenVaciaSeRechaza()
    {
        Assert.Throws<InvoiceRuleViolationException>(
            () => ProductImage.Create(Guid.NewGuid(), [], ImageFormat.Jpeg, Instante));
    }

    [Fact]
    public void UnaImagenSinProductoSeRechaza()
    {
        Assert.Throws<InvoiceRuleViolationException>(
            () => ProductImage.Create(Guid.Empty, Contenido("abc"), ImageFormat.Jpeg, Instante));
    }

    [Fact]
    public void EntrarEnElConjuntoAsignaLaRevisionYLaDejaVigente()
    {
        var imagen = ProductImage.Create(Guid.NewGuid(), Contenido("abc"), ImageFormat.Jpeg, Instante);

        imagen.Enter(7);

        Assert.Equal(7, imagen.RevisionAdded);
        Assert.Null(imagen.RevisionRemoved);
        Assert.True(imagen.IsLive);
    }

    [Fact]
    public void RetirarLaImagenLaSacaDelConjunto()
    {
        var imagen = ProductImage.Create(Guid.NewGuid(), Contenido("abc"), ImageFormat.Jpeg, Instante);
        imagen.Enter(7);

        imagen.Retire(9, Instante);

        Assert.Equal(9, imagen.RevisionRemoved);
        Assert.Equal(Instante, imagen.RemovedAtUtc);
        Assert.False(imagen.IsLive);
    }

    [Fact]
    public void RetirarUnaImagenYaRetiradaSeRechaza()
    {
        var imagen = ProductImage.Create(Guid.NewGuid(), Contenido("abc"), ImageFormat.Jpeg, Instante);
        imagen.Enter(7);
        imagen.Retire(9, Instante);

        Assert.Throws<InvoiceRuleViolationException>(() => imagen.Retire(11, Instante));
    }

    [Fact]
    public void EntrarEnElConjuntoConRevisionInvalidaSeRechaza()
    {
        var imagen = ProductImage.Create(Guid.NewGuid(), Contenido("abc"), ImageFormat.Jpeg, Instante);

        Assert.Throws<InvoiceRuleViolationException>(() => imagen.Enter(0));
    }

    [Fact]
    public void ElDetectorReconoceLosTresFormatosAdmitidos()
    {
        Assert.Equal(ImageFormat.Jpeg, ImageFormatDetector.Detect(JpegMinimo()));
        Assert.Equal(ImageFormat.Png, ImageFormatDetector.Detect(PngMinimo()));
        Assert.Equal(ImageFormat.WebP, ImageFormatDetector.Detect(WebPMinimo()));
    }

    [Fact]
    public void ElDetectorNoReconoceUnArchivoQueNoEsUnaImagen()
    {
        Assert.Null(ImageFormatDetector.Detect(Contenido("esto es un texto")));
        Assert.Null(ImageFormatDetector.Detect([]));
    }

    [Fact]
    public void ElDetectorIgnoraElContentTypeDeclarado()
    {
        // El archivo tiene los bytes magicos del PNG: da igual lo que declare
        // la peticion, el formato es el que dicen los bytes.
        Assert.Equal(ImageFormat.Png, ImageFormatDetector.Detect(PngMinimo()));
    }

    [Fact]
    public void CadaFormatoTieneSuContentTypeYSuExtension()
    {
        Assert.Equal("image/jpeg", ImageFormatDetector.ContentTypeOf(ImageFormat.Jpeg));
        Assert.Equal("image/png", ImageFormatDetector.ContentTypeOf(ImageFormat.Png));
        Assert.Equal("image/webp", ImageFormatDetector.ContentTypeOf(ImageFormat.WebP));
        Assert.Equal("jpg", ImageFormatDetector.ExtensionOf(ImageFormat.Jpeg));
        Assert.Equal("png", ImageFormatDetector.ExtensionOf(ImageFormat.Png));
        Assert.Equal("webp", ImageFormatDetector.ExtensionOf(ImageFormat.WebP));
    }

    private static byte[] Contenido(string texto) =>
        System.Text.Encoding.UTF8.GetBytes(texto);

    private static byte[] JpegMinimo() =>
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    private static byte[] PngMinimo() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

    private static byte[] WebPMinimo() =>
        [0x52, 0x49, 0x46, 0x46, 0x1A, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20];
}
