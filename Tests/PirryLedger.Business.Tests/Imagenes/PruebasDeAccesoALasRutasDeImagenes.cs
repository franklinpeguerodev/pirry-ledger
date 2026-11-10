using System.Net;
using System.Net.Http.Headers;
using System.Text;
using PirryLedger.Business.Tests.Operaciones;

namespace PirryLedger.Business.Tests.Imagenes;

// El acceso por rol de las rutas de las imagenes del catalogo, probado sobre
// HTTP (RD-06, RF-CA-05).
//
// Estas pruebas no comprueban un caso de uso: comprueban la FRONTERA. Que la
// peticion se rechace en el servidor, antes de que la ruta toque el producto,
// el bucket o la base, y que el rechazo no deje nada a medias.
//
// La tabla de roles ya la comprueban las pruebas del punto unico de acceso, en
// la suite de AccessControl. Lo que faltaba era esto: la peticion de verdad,
// con su cabecera Authorization, contra la ruta de verdad.
public sealed class PruebasDeAccesoALasRutasDeImagenes
{
    private const string Subida = "/api/admin/products/{0}/image";

    private const string Sincronizacion = "/api/billing/product-images/sync";

    [Fact]
    public async Task UnaSubidaSinSesionResponde401YNoCambiaNada()
    {
        await using var servidor = new ServidorDePruebasHttp();
        var producto = servidor.CrearProducto();

        var respuesta = await servidor.Cliente.SendAsync(
            PeticionDeSubida(producto.Id, FormularioConJpeg()));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        // Nada cambio: no se subio nada al bucket y no hay imagen del producto.
        Assert.Empty(servidor.Store.Uploads);
        Assert.Null(await servidor.Imagenes.FindLiveAsync(producto.Id));
    }

    [Fact]
    public async Task UnaSubidaDeUnEstandarResponde403YNoCambiaNada()
    {
        await using var servidor = new ServidorDePruebasHttp();
        var producto = servidor.CrearProducto();

        var respuesta = await servidor.Cliente.SendAsync(
            PeticionDeSubida(producto.Id, FormularioConJpeg(), servidor.TokenDeStandard()));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        Assert.Empty(servidor.Store.Uploads);
        Assert.Null(await servidor.Imagenes.FindLiveAsync(producto.Id));
    }

    [Fact]
    public async Task UnaSubidaDeUnEstandarSeRechazaAntesDeLeerElCuerpo()
    {
        await using var servidor = new ServidorDePruebasHttp();
        var producto = servidor.CrearProducto();

        // Un archivo de mas de 5 MiB. Si la ruta leyera el cuerpo antes de
        // comprobar el rol, la respuesta seria 413 (el archivo no cabe) y no
        // 403: el rechazo por rol tiene que ocurrir antes.
        var respuesta = await servidor.Cliente.SendAsync(
            PeticionDeSubida(
                producto.Id,
                FormularioConBytes(new byte[6 * 1024 * 1024]),
                servidor.TokenDeStandard()));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Empty(servidor.Store.Uploads);
    }

    [Fact]
    public async Task UnaSubidaDeUnAdministradorSubeElArchivo()
    {
        await using var servidor = new ServidorDePruebasHttp();
        var producto = servidor.CrearProducto();

        var respuesta = await servidor.Cliente.SendAsync(
            PeticionDeSubida(producto.Id, FormularioConJpeg(), servidor.TokenDeAdministrator()));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        // El control de esta prueba: si el rol no dejara pasar al
        // Administrator, las pruebas de 401 y 403 no probarian nada.
        var subida = Assert.Single(servidor.Store.Uploads);
        Assert.Equal("image/jpeg", subida.ContentType);

        var imagen = await servidor.Imagenes.FindLiveAsync(producto.Id);
        Assert.NotNull(imagen);
        Assert.Equal(subida.Key, imagen.ImageKey);
    }

    [Fact]
    public async Task UnaSubidaAUnProductoQueNoExisteResponde404SinLeerElCuerpo()
    {
        await using var servidor = new ServidorDePruebasHttp();

        // El cuerpo NO es un formulario. Si la ruta lo leyera primero, la
        // respuesta seria 415 ("la peticion debe ser un formulario"). Como el
        // producto no existe, la respuesta es 404: el servidor no leyo el
        // cuerpo.
        var respuesta = await servidor.Cliente.SendAsync(
            PeticionDeSubida(
                Guid.NewGuid(),
                new StringContent("esto no es un formulario"),
                servidor.TokenDeAdministrator()));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Empty(servidor.Store.Uploads);
    }

    [Fact]
    public async Task UnaSubidaAUnProductoInactivoResponde400SinLeerElCuerpo()
    {
        await using var servidor = new ServidorDePruebasHttp();
        var producto = servidor.CrearProducto();
        producto.SetActive(false);

        // Mismo criterio que el 404: el cuerpo no se lee cuando el producto no
        // puede recibir la imagen.
        var respuesta = await servidor.Cliente.SendAsync(
            PeticionDeSubida(
                producto.Id,
                new StringContent("esto no es un formulario"),
                servidor.TokenDeAdministrator()));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Empty(servidor.Store.Uploads);
    }

    [Fact]
    public async Task QuitarLaImagenDeUnEstandarResponde403YLaImagenSigueVigente()
    {
        await using var servidor = new ServidorDePruebasHttp();
        var producto = servidor.CrearProducto();

        await servidor.Cliente.SendAsync(
            PeticionDeSubida(producto.Id, FormularioConJpeg(), servidor.TokenDeAdministrator()));

        var antes = await servidor.Imagenes.FindLiveAsync(producto.Id);
        Assert.NotNull(antes);

        var peticion = new HttpRequestMessage(
            HttpMethod.Delete,
            string.Format(Subida, producto.Id));

        peticion.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            servidor.TokenDeStandard());

        var respuesta = await servidor.Cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        // La imagen sigue vigente y su archivo sigue en el bucket.
        var despues = await servidor.Imagenes.FindLiveAsync(producto.Id);
        Assert.NotNull(despues);
        Assert.Equal(antes.ImageKey, despues.ImageKey);
        Assert.Empty(servidor.Store.Deleted);
    }

    [Fact]
    public async Task UnaSincronizacionSinSesionResponde401()
    {
        await using var servidor = new ServidorDePruebasHttp();

        var respuesta = await servidor.Cliente.SendAsync(PeticionDeSincronizacion());

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Empty(servidor.Store.Signed);
    }

    [Fact]
    public async Task UnaSincronizacionDeUnEstandarResponde200()
    {
        await using var servidor = new ServidorDePruebasHttp();
        var producto = servidor.CrearProducto();

        await servidor.Cliente.SendAsync(
            PeticionDeSubida(producto.Id, FormularioConJpeg(), servidor.TokenDeAdministrator()));

        // Cualquier sesion valida puede sincronizar: el mostrador necesita las
        // fotos para cobrar.
        var respuesta = await servidor.Cliente.SendAsync(
            PeticionDeSincronizacion(servidor.TokenDeStandard()));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var firmado = Assert.Single(servidor.Store.Signed);
        Assert.Equal("image/jpeg", firmado.ContentType);
    }

    [Fact]
    public async Task LaSincronizacionSeRechazaCon429DespuesDeVeinteSolicitudes()
    {
        await using var servidor = new ServidorDePruebasHttp();
        var token = servidor.TokenDeStandard();

        for (var intento = 0; intento < 20; intento++)
        {
            var permitida = await servidor.Cliente.SendAsync(PeticionDeSincronizacion(token));

            Assert.Equal(HttpStatusCode.OK, permitida.StatusCode);
        }

        var rechazo = await servidor.Cliente.SendAsync(PeticionDeSincronizacion(token));

        Assert.Equal(HttpStatusCode.TooManyRequests, rechazo.StatusCode);
        Assert.NotNull(rechazo.Headers.RetryAfter);
    }

    // --- Utilidades --------------------------------------------------------

    private static HttpRequestMessage PeticionDeSubida(
        Guid productoId,
        HttpContent contenido,
        string? token = null)
    {
        var peticion = new HttpRequestMessage(
            HttpMethod.Put,
            string.Format(Subida, productoId))
        {
            Content = contenido,
        };

        if (token is not null)
        {
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return peticion;
    }

    private static HttpRequestMessage PeticionDeSincronizacion(string? token = null)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, Sincronizacion)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };

        if (token is not null)
        {
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return peticion;
    }

    private static MultipartFormDataContent FormularioConJpeg() =>
        FormularioConBytes(Jpeg());

    private static MultipartFormDataContent FormularioConBytes(byte[] contenido)
    {
        var formulario = new MultipartFormDataContent();

        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        formulario.Add(archivo, "file", "combo.jpg");

        return formulario;
    }

    // Un JPEG minimo pero valido: SOI, JFIF, DQT, SOS, datos y EOI.
    private static byte[] Jpeg()
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange(Segment(0xE0, [.. "JFIF\0"u8, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]));
        bytes.AddRange(Segment(0xDB, [0x00, 0x01, 0x02, 0x03]));
        bytes.AddRange(Segment(0xDA, [0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]));
        bytes.AddRange([0x11, 0x22, 0x33, 0x44]);
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
