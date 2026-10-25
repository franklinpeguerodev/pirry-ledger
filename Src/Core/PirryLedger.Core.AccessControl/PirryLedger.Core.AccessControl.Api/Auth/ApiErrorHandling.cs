using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PirryLedger.Core.AccessControl.Application;

namespace PirryLedger.Core.AccessControl.Api;

// La frontera de error del API.
//
// Hay dos caminos por los que una peticion mal formada llega aqui, y los dos
// tienen que acabar igual:
//
// 1. En desarrollo, la pagina de excepciones del framework activa
//    ThrowOnBadRequest, asi que un cuerpo que no se puede leer lanza una
//    BadHttpRequestException. Este middleware la captura y responde 400.
//
// 2. En produccion no hay pagina de excepciones, el framework convierte el
//    fallo de lectura en un 400 sin cuerpo y no lanza nada. Este middleware
//    lo ve al volver de la ruta y escribe el cuerpo con el mismo formato.
//
// El resultado es el mismo en los dos entornos: 400 con {"message": "..."},
// sin traza de pila, sin ruta de fichero, sin consulta SQL y sin la cabecera
// Authorization del cliente (RD-07, RD-08).
//
// Vive en la pieza de AccessControl porque es la pieza que ya define el
// formato de error (ErrorResponse) y el filtro de acceso (AccessFilter), y
// porque Permissions usa las dos. El Host solo lo registra: el Host es
// composicion y no decide reglas.
public static class ApiErrorHandling
{
    // Los mensajes no dicen nada del interior: ni el tipo que se esperaba, ni
    // la ruta del fichero, ni el contenido recibido.
    private const string InvalidBodyMessage = "El cuerpo de la peticion no es valido.";
    private const string UnsupportedMediaTypeMessage = "El tipo de contenido debe ser application/json.";
    private const string UnexpectedMessage = "Se produjo un error inesperado.";

    public static async Task HandleErrorsAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException)
        {
            // El cuerpo no se pudo leer. La excepcion no se registra con su
            // mensaje porque puede contener fragmentos del cuerpo recibido.
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, InvalidBodyMessage);
        }
        catch (OperationNotDeclaredException)
        {
            // Fallo de programacion: una ruta de negocio sin OperationMetadata.
            // No es culpa del cliente, asi que se responde 500 con un mensaje
            // generico.
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, UnexpectedMessage);
        }
        catch (OperationWithoutRoleRequirementException)
        {
            // Fallo de programacion: una operacion en la tabla sin nivel de
            // acceso valido.
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, UnexpectedMessage);
        }
        catch (Exception error)
        {
            // Cualquier otra excepcion es un fallo no previsto. Se registra
            // en el servidor (tipo y mensaje, sin cabeceras ni cuerpo de la
            // peticion) y el cliente recibe un mensaje generico.
            context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("PirryLedger.ApiErrorHandling")
                .LogError(error, "Fallo no controlado al procesar {Path}.", context.Request.Path);

            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, UnexpectedMessage);
        }

        await WriteFrameworkErrorBodyAsync(context);
    }

    // El framework responde con un status de error y sin cuerpo en dos casos
    // que nos tocan: un Content-Type que la ruta no admite (415) y un cuerpo
    // que no se puede leer (400, solo en produccion, porque en desarrollo el
    // caso 1 de arriba ya lo ha resuelto). Se escribe el cuerpo con el
    // formato del API para que el cliente reciba siempre {"message": ...}.
    //
    // El 404 de una ruta que no existe NO se toca: no es un error del API y
    // su respuesta sin cuerpo es la que documenta el README.
    private static async Task WriteFrameworkErrorBodyAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var message = context.Response.StatusCode switch
        {
            StatusCodes.Status400BadRequest => InvalidBodyMessage,
            StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaTypeMessage,
            _ => null,
        };

        if (message is null)
        {
            return;
        }

        await context.Response.WriteAsJsonAsync(new ErrorResponse(message));
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            // La respuesta ya empezo: no se puede cambiar el status ni el
            // cuerpo. Se deja como esta para no romper lo ya escrito.
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(message));
    }
}
