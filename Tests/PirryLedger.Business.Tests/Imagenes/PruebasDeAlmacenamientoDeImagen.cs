using PirryLedger.Business.Infrastructure.Storage;

namespace PirryLedger.Business.Tests.Imagenes;

// El almacenamiento de imagenes (ADR 015). Aqui se prueba la firma, que es lo
// unico que se puede comprobar sin una cuenta de Cloudflare: firmar es un
// calculo local y no toca la red.
public sealed class PruebasDeAlmacenamientoDeImagen
{
    private const string Bucket = "pirry-imagenes";
    private const string Clave = "products/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222.jpg";

    [Fact]
    public void LaFirmaDeDescargaLlevaElBucketLaClaveYElTipoValidado()
    {
        var store = Store();

        var url = Uri.UnescapeDataString(
            store.CreateDownloadUrl(Clave, "image/jpeg", DateTime.UtcNow.AddMinutes(15)));

        Assert.Contains(Bucket, url);
        Assert.Contains(Clave, url);
        Assert.Contains("response-content-type=image/jpeg", url);
    }

    [Fact]
    public void LaFirmaCaducaEnElTiempoIndicado()
    {
        var store = Store();

        var url = Uri.UnescapeDataString(
            store.CreateDownloadUrl(Clave, "image/jpeg", DateTime.UtcNow.AddMinutes(15)));

        var segundos = LeerSegundosDeCaducidad(url);
        Assert.InRange(segundos, 895, 900);
    }

    [Fact]
    public void LaFirmaTraeLaFirmaDeAwsYEl_bucketPrivadoNoSeAbre()
    {
        var store = Store();

        var url = store.CreateDownloadUrl(Clave, "image/jpeg", DateTime.UtcNow.AddMinutes(15));

        // Sin los parametros de la firma, la URL no sirve para descargar nada.
        Assert.Contains("X-Amz-Signature=", url);
        Assert.Contains("X-Amz-Expires=", url);
        Assert.Contains("X-Amz-Credential=", url);
    }

    private static R2ImageStore Store() =>
        new(new ImageStorageConfiguration(
            AccountId: "cuenta-de-prueba",
            AccessKeyId: "clave-de-prueba",
            SecretAccessKey: "secreto-de-prueba",
            Bucket: Bucket,
            Endpoint: "https://cuenta-de-prueba.r2.cloudflarestorage.com",
            SignatureMinutes: 15,
            MaxBytes: 5_242_880));

    private static int LeerSegundosDeCaducidad(string url)
    {
        const string marca = "X-Amz-Expires=";
        var inicio = url.IndexOf(marca, StringComparison.Ordinal) + marca.Length;
        var fin = url.IndexOf('&', inicio);
        var valor = fin < 0 ? url[inicio..] : url[inicio..fin];

        return int.Parse(valor);
    }
}
