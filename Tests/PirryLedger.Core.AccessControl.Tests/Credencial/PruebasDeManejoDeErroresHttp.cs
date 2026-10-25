using PirryLedger.Core.AccessControl.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RD-07 y RD-08: la entrada externa se valida antes de usarse y el error
// devuelto no expone trazas, rutas de fichero, consultas SQL ni cabeceras de
// la peticion.
//
// Estas pruebas fijan la frontera de error del API sin levantar el Host ni
// PostgreSQL: se invoca el middleware con un DefaultHttpContext y un
// RequestDelegate que hace lo que hace el framework en cada caso.
//
// El caso que mas importa es el primero: sin el middleware, un cuerpo que no
// se puede leer deja volar una BadHttpRequestException hasta la pagina de
// excepciones del entorno de desarrollo, y el cliente recibe un 400 con la
// traza de pila, la ruta absoluta del proyecto y la cabecera Authorization
// con su propio token.
public sealed class PruebasDeManejoDeErroresHttp
{
    [Fact]
    public async Task UnCuerpoQueNoSePuedeLeerResponde400ConMensajeControlado()
    {
        // Arrange: el framework lanza BadHttpRequestException al leer un
        // cuerpo que no es JSON valido.
        var (context, cuerpo) = CrearContexto();
        var middleware = CrearMiddlewareQueLanza(new BadHttpRequestException("detalle interno"));

        // Act
        await middleware(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var respuesta = LeerCuerpo(cuerpo);

        Assert.Contains("El cuerpo de la peticion no es valido.", respuesta, StringComparison.Ordinal);

        // RD-08: nada de lo que sale hacia el cliente revela el interior.
        Assert.DoesNotContain("detalle interno", respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain(":\\", respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnCuerpoRechazadoPorElFrameworkResponde400ConMensajeControlado()
    {
        // Arrange: en produccion no hay pagina de excepciones, asi que el
        // framework no lanza: responde 400 y vuelve sin cuerpo. Es el mismo
        // fallo que el caso anterior, resuelto por otro camino.
        var (context, cuerpo) = CrearContexto();
        var middleware = CrearMiddlewareQueResponde(StatusCodes.Status400BadRequest);

        // Act
        await middleware(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains(
            "El cuerpo de la peticion no es valido.",
            LeerCuerpo(cuerpo),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnContentTypeNoAdmitidoResponde415ConElFormatoDelApi()
    {
        // Arrange: el endpoint del framework responde 415 sin cuerpo.
        var (context, cuerpo) = CrearContexto();
        var middleware = CrearMiddlewareQueResponde(StatusCodes.Status415UnsupportedMediaType);

        // Act
        await middleware(context);

        // Assert
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, context.Response.StatusCode);
        Assert.Contains(
            "El tipo de contenido debe ser application/json.",
            LeerCuerpo(cuerpo),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnFalloNoPrevistoResponde500ConMensajeGenerico()
    {
        // Arrange: una excepcion que el caso de uso no controla.
        var (context, cuerpo) = CrearContexto();
        var middleware = CrearMiddlewareQueLanza(new InvalidOperationException("detalle interno"));

        // Act
        await middleware(context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        var respuesta = LeerCuerpo(cuerpo);

        Assert.Contains("Se produjo un error inesperado.", respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("detalle interno", respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnaRutaDelFrameworkNoSeConfundeConUnaRutaSinOperacion()
    {
        // Arrange: cuando el Content-Type no es el que la ruta admite, el
        // enrutador selecciona un endpoint del framework (un Endpoint sin
        // RoutePattern) que responde 415. Ese endpoint no declara operacion
        // porque no es una ruta de negocio, y el middleware de acceso no puede
        // confundirlo con "una ruta nuestra sin operacion declarada".
        var (context, _) = CrearContexto();
        context.Request.Path = "/api/permissions/requests";
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(),
            "415 HTTP Unsupported Media Type"));

        var siguienteSeLlamo = false;

        // Act
        await AccessFilter.RequireDeclaredOperationAsync(
            context,
            _ =>
            {
                siguienteSeLlamo = true;
                return Task.CompletedTask;
            });

        // Assert: no lanza, y deja pasar la peticion al framework.
        Assert.True(siguienteSeLlamo);
    }

    // El contenido escrito en el cuerpo de la respuesta. MemoryStream.ToString()
    // devuelve el nombre del tipo, no lo escrito: hay que leer los bytes.
    private static string LeerCuerpo(MemoryStream cuerpo) =>
        Encoding.UTF8.GetString(cuerpo.ToArray());

    private static (DefaultHttpContext context, MemoryStream cuerpo) CrearContexto()    {
        var cuerpo = new MemoryStream();

        var servicios = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = servicios,
        };

        context.Response.Body = cuerpo;

        return (context, cuerpo);
    }

    private static Func<HttpContext, Task> CrearMiddlewareQueLanza(Exception error) =>
        context => ApiErrorHandling.HandleErrorsAsync(context, _ => throw error);

    private static Func<HttpContext, Task> CrearMiddlewareQueResponde(int statusCode) =>
        context => ApiErrorHandling.HandleErrorsAsync(context, ctx =>
        {
            ctx.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        });
}
