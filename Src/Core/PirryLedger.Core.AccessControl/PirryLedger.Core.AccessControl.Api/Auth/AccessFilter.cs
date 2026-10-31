using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace PirryLedger.Core.AccessControl.Api;

// RF-CA-05: lo que hace que la tabla sea la fuente de verdad.
//
// El endpoint se registra diciendo QUE operacion es (WithAccess) y no quien puede
// usarla. El filtro es quien va a la tabla a averiguarlo. Asi el endpoint no
// repite la exigencia, y una ruta sin declarar no se abre.
//
// Por que un filtro y no un if en cada endpoint: un if obligaria a repetir el
// mismo esquema en cada ruta y bastaria con olvidar uno. Ademas, el filtro se
// anade DENTRO de WithAccess, asi que es imposible declarar una operacion y no
// pasar por el filtro.
public static class AccessFilter
{
    // Registra la ruta declarando que operacion es. El endpoint no dice nada mas
    // sobre acceso: eso sale de RoleRequirements.
    public static RouteHandlerBuilder WithAccess(
        this RouteHandlerBuilder builder,
        Operation operation)
    {
        builder.Add(b => b.Metadata.Add(new OperationMetadata(operation)));
        builder.AddEndpointFilter(ApplyAsync);

        return builder;
    }

    // El filtro. Se ejecuta antes que el codigo del endpoint, de modo que un
    // rechazo de role ocurre en el SERVIDOR (RD-06) sin llegar a la logica.
    public static async ValueTask<object?> ApplyAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var operation = context.HttpContext.GetEndpoint()?
            .Metadata.GetMetadata<OperationMetadata>()?.Operation;

        // Sin declaracion no se ejecuta nada. Es el reverso de "si no esta
        // declarado, se abre": aqui una ruta sin declarar se cierra.
        if (operation is null)
        {
            throw new OperationNotDeclaredException(
                context.HttpContext.GetEndpoint()?.DisplayName ?? "(ruta sin nombre)");
        }

        var level = RoleRequirements.RequiredLevel(operation.Value);

        if (level == AccessLevel.Public)
        {
            return await next(context);
        }

        var plainToken = ReadToken(context.HttpContext);

        // Logout es de "cualquier sesion" pero tolera una credencial que no
        // sea valida: responde 204 siempre para no decir si el token existio
        // alguna vez (RF-CA-18). La tolerancia se lee de la tabla, no se decide
        // aqui: si estuviera en el filtro, el endpoint y el filtro contarian dos
        // veces la misma regla y volveriamos al problema que la tabla evita.
        if (RoleRequirements.ToleratesInvalidCredential(operation.Value))
        {
            return await next(context);
        }

        if (string.IsNullOrEmpty(plainToken))
        {
            return InvalidSession();
        }

        var authenticate = context.HttpContext.RequestServices.GetRequiredService<Authenticate>();

        if (level == AccessLevel.AnySession)
        {
            var sessionRejection = await ValidateSessionAsync(context, authenticate, plainToken);

            return sessionRejection ?? await next(context);
        }

        // RF-GP-05: la operation exige un permiso concedido. La pieza de
        // Permissions implementa IPermissionAuthority (consulta per_permission_grants);
        // el filtro solo conoce el contrato. Authenticate distingue el 401 del 403:
        // primero la sesion, despues el permiso (mismo orden que con role).
        if (level == AccessLevel.Permission)
        {
            var permissionRejection = await ValidatePermissionAsync(
                context,
                authenticate,
                plainToken,
                RoleRequirements.RequiredPermission(operation.Value));

            return permissionRejection ?? await next(context);
        }

        // Standard o Administrator. Authenticate distingue el 401 del 403: primero
        // comprueba la sesion y despues el role (RF-CA-06). El role concreto sale
        // de la tabla, nunca se escribe aqui.
        var roleRejection = await ValidateRoleAsync(
            context,
            authenticate,
            plainToken,
            RoleRequirements.RequiredRole(operation.Value));

        return roleRejection ?? await next(context);
    }

    // El filtro corre ANTES que el caso de uso, asi que es el primero que ve las
    // excepciones de Authenticate y el primero que tiene que traducirlas. Sin esto,
    // InvalidSessionException e InsufficientRoleException saldrian como 500 con
    // traza (RD-07 y RD-08), y las rutas de administracion perderian el 401 y el
    // 403 que antes devolvia el endpoint.
    //
    // Devuelven null cuando todo va bien, y el rechazo ya construido cuando no.
    private static async Task<IResult?> ValidateSessionAsync(
        EndpointFilterInvocationContext context,
        Authenticate authenticate,
        string plainToken)
    {
        try
        {
            await authenticate.ExecuteAsync(plainToken, context.HttpContext.RequestAborted);
            return null;
        }
        catch (InvalidSessionException error)
        {
            return AccessDenied(error.Message, isRoleShortage: false);
        }
    }

    private static async Task<IResult?> ValidateRoleAsync(
        EndpointFilterInvocationContext context,
        Authenticate authenticate,
        string plainToken,
        Role role)
    {
        try
        {
            await authenticate.ExecuteWithRoleAsync(
                plainToken,
                role,
                context.HttpContext.RequestAborted);

            return null;
        }
        catch (InvalidSessionException error)
        {
            // El 401 tiene que salir antes que el 403: primero se comprueba la
            // sesion, despues el role.
            return AccessDenied(error.Message, isRoleShortage: false);
        }
        catch (InsufficientRoleException error)
        {
            return AccessDenied(error.Message, isRoleShortage: true);
        }
    }

    // RF-GP-05: misma forma que ValidateRoleAsync pero para AccessLevel.Permission.
    // El 401 sale antes que el 403 porque sin sesion no se puede saber que
    // permisos tiene.
    private static async Task<IResult?> ValidatePermissionAsync(
        EndpointFilterInvocationContext context,
        Authenticate authenticate,
        string plainToken,
        string permissionName)
    {
        try
        {
            await authenticate.ExecuteWithPermissionAsync(
                plainToken,
                permissionName,
                context.HttpContext.RequestAborted);

            return null;
        }
        catch (InvalidSessionException error)
        {
            return AccessDenied(error.Message, isRoleShortage: false);
        }
        catch (InsufficientPermissionException error)
        {
            return AccessDenied(error.Message, isRoleShortage: true);
        }
    }

    // El middleware que convierte "una ruta sin declarar no se abre" en algo que
    // se cumple en cada peticion, y no solo en una prueba.
    //
    // El filtro protege las rutas que se declararon. Este cubre el otro lado: si
    // alguien anade un MapGet y olvida el WithAccess, el filtro no esta y la ruta
    // se abriria sin comprobar nada. Aqui se rechaza antes de ejecutar la logica.
    //
    // Por que lanza y no devuelve 403: es un fallo de programacion, no una
    // peticion invalida. Un 403 diria al cliente que su credencial es la que no
    // vale, y no es verdad.
    public static async Task RequireDeclaredOperationAsync(
        HttpContext context,
        RequestDelegate next)
    {
        var endpoint = context.GetEndpoint();

        // Sin endpoint resuelto no hay ruta que ejecutar. Y las que no son de
        // negocio (OpenApi) se dejan pasar: no tienen operacion de negocio.
        if (endpoint is null || !IsBusinessRoute(context.Request.Path))
        {
            await next(context);
            return;
        }

        // Un endpoint sin OperationMetadata puede ser una respuesta del propio
        // framework y no una ruta nuestra: cuando el Content-Type de la
        // peticion no es el que la ruta admite, el enrutador selecciona un
        // endpoint propio del framework (un Microsoft.AspNetCore.Http.Endpoint
        // sin RoutePattern) que responde 415 Unsupported Media Type, y otro
        // para 405 Method Not Allowed. Esos endpoints no son rutas de negocio
        // y no declaran operacion porque no la tienen: se dejan pasar para que
        // el framework conteste con su status. Antes esto devolvia un 500 con
        // un mensaje que culpaba al cliente de algo que no habia hecho.
        if (endpoint is not RouteEndpoint)
        {
            await next(context);
            return;
        }

        if (endpoint.Metadata.GetMetadata<OperationMetadata>() is null)
        {
            throw new OperationNotDeclaredException(
                endpoint.DisplayName ?? "(ruta sin nombre)");
        }

        await next(context);
    }

    // Heuristica explicita: las rutas de negocio son las que expone este modulo.
    // Se escribe aqui en vez de inventar un marcador en cada ruta para que el
    // nucleo no dependa de las rutas de OpenApi.
    private static bool IsBusinessRoute(PathString path) =>
        path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/me", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/activate", StringComparison.OrdinalIgnoreCase);

    private static string? ReadToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = header[prefix.Length..].Trim();

        return string.IsNullOrEmpty(token) ? null : token;
    }

    private static IResult InvalidSession() => AccessDenied("Sesion no valida.", isRoleShortage: false);

    // El 401 y el 403 salen de aqui y no del caso de uso, porque el filtro corre
    // antes. El cuerpo es el mismo que usaba el endpoint: {"mensaje": "..."}.
    private static IResult AccessDenied(string message, bool isRoleShortage) =>
        Results.Json(
            new ErrorResponse(message),
            statusCode: isRoleShortage
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized);

    // Metadato: dice que operacion es la ruta. Vive en la lista de metadatos del
    // endpoint para que la prueba que recorre las rutas reales del Host lo pueda
    // leer y compararlo con la tabla.
    public sealed class OperationMetadata(Operation operation)
    {
        public Operation Operation { get; } = operation;
    }
}

// Distinta de OperationWithoutRoleRequirementException a proposito: una es "esta
// operacion no esta en la tabla" y la otra es "esta ruta no dijo que operacion
// es". Las dos son fallos de programacion, no peticiones invalidas del cliente.
public sealed class OperationNotDeclaredException(string route)
    : InvalidOperationException($"La ruta {route} no declara su operacion en el filtro de acceso.");
