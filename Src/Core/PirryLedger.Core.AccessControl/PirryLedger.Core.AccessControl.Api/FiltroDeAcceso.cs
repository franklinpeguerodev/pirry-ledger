using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.AccessControl.Application;

namespace PirryLedger.Core.AccessControl.Api;

// RF-CA-05: lo que hace que la tabla sea la fuente de verdad.
//
// El endpoint se registra diciendo QUAL operacion es (ConAcceso) y no quien puede
// usarla. El filtro es quien va a la tabla a averiguarlo. Asi el endpoint no
// repite la exigencia, y una ruta sin declarar no se abre.
//
// Por que un filtro y no un if en cada endpoint: un if obligaria a repetir el
// mismo esquema en cada ruta y bastaria con olvidar uno. Ademas, el filtro se
// anade DENTRO de ConAcceso, asi que es imposible declarar una operacion y no
// pasar por el filtro.
public static class FiltroDeAcceso
{
    // Registra la ruta declarando que operacion es. El endpoint no dice nada mas
    // sobre acceso: eso sale de ExigenciasDeRol.
    public static RouteHandlerBuilder ConAcceso(
        this RouteHandlerBuilder builder,
        Operacion operacion)
    {
        builder.Add(b => b.Metadata.Add(new MetadatoDeOperacion(operacion)));
        builder.AddEndpointFilter(AplicarAsync);

        return builder;
    }

    // El filtro. Se ejecuta antes que el codigo del endpoint, de modo que un
    // rechazo de rol ocurre en el SERVIDOR (RD-06) sin llegar a la logica.
    public static async ValueTask<object?> AplicarAsync(
        EndpointFilterInvocationContext contexto,
        EndpointFilterDelegate siguiente)
    {
        var operacion = contexto.HttpContext.GetEndpoint()?
            .Metadata.GetMetadata<MetadatoDeOperacion>()?.Operacion;

        // Sin declaracion no se ejecuta nada. Es el reverso de "si no esta
        // declarado, se abre": aqui una ruta sin declarar se cierra.
        if (operacion is null)
        {
            throw new OperacionSinDeclararException(
                contexto.HttpContext.GetEndpoint()?.DisplayName ?? "(ruta sin nombre)");
        }

        var nivel = ExigenciasDeRol.NivelRequerido(operacion.Value);

        if (nivel == NivelAcceso.Publico)
        {
            return await siguiente(contexto);
        }

        var tokenEnClaro = LeerToken(contexto.HttpContext);

        // Cerrar sesion es de "cualquier sesion" pero tolera una credencial que no
        // sea valida: responde 204 siempre para no decir si el token existo
        // alguna vez (RF-CA-18). La tolerancia se lee de la tabla, no se decide
        // aqui: si estuviera en el filtro, el endpoint y el filtro contarian dos
        // veces la misma regla y volveriamos al problema que la tabla evita.
        if (ExigenciasDeRol.ToleraCredencialInvalida(operacion.Value))
        {
            return await siguiente(contexto);
        }

        if (string.IsNullOrEmpty(tokenEnClaro))
        {
            return SesionNoValida();
        }

        var autenticador = contexto.HttpContext.RequestServices.GetRequiredService<Autenticar>();

        if (nivel == NivelAcceso.CualquierSesion)
        {
            var rechazoSesion = await ValidarSesionAsync(contexto, autenticador, tokenEnClaro!);

            return rechazoSesion ?? await siguiente(contexto);
        }

        // Administrador. Autenticar distingue el 401 del 403: primero comprueba la
        // sesion y despues el rol (RF-CA-06).
        var rechazoRol = await ValidarRolAsync(
            contexto,
            autenticador,
            tokenEnClaro!,
            ExigenciasDeRol.RolRequerido(operacion.Value));

        return rechazoRol ?? await siguiente(contexto);
    }

    // El filtro corre ANTES que el caso de uso, asi que es el primero que ve las
    // excepciones de Autenticar y el primero que tiene que traducirlas. Sin esto,
    // SesionInvalidaException y RolInsuficienteException saldrian como 500 con
    // traza (RD-07 y RD-08), y las rutas de administracion perderian el 401 y el
    // 403 que antes devolvia el endpoint.
    //
    // Devuelven null cuando todo va bien, y el rechazo ya construido cuando no.
    private static async Task<IResult?> ValidarSesionAsync(
        EndpointFilterInvocationContext contexto,
        Autenticar autenticador,
        string tokenEnClaro)
    {
        try
        {
            await autenticador.EjecutarAsync(tokenEnClaro, contexto.HttpContext.RequestAborted);
            return null;
        }
        catch (SesionInvalidaException error)
        {
            return AccesoDenegado(error.Message, esFaltaDeRol: false);
        }
    }

    private static async Task<IResult?> ValidarRolAsync(
        EndpointFilterInvocationContext contexto,
        Autenticar autenticador,
        string tokenEnClaro,
        Domain.Rol rol)
    {
        try
        {
            await autenticador.EjecutarConRolAsync(
                tokenEnClaro,
                rol,
                contexto.HttpContext.RequestAborted);

            return null;
        }
        catch (SesionInvalidaException error)
        {
            // El 401 tiene que salir antes que el 403: primero se comprueba la
            // sesion, despues el rol.
            return AccesoDenegado(error.Message, esFaltaDeRol: false);
        }
        catch (RolInsuficienteException error)
        {
            return AccesoDenegado(error.Message, esFaltaDeRol: true);
        }
    }

    // El middleware que convierte "una ruta sin declarar no se abre" en algo que
    // se cumple en cada peticion, y no solo en una prueba.
    //
    // El filtro protege las rutas que se declararon. Este cubre el otro lado: si
    // alguien anade un MapGet y olvida el ConAcceso, el filtro no esta y la ruta
    // se abriria sin comprobar nada. Aqui se rechaza antes de ejecutar la logica.
    //
    // Por que lanza y no devuelve 403: es un fallo de programacion, no una
    // peticion invalida. Un 403 diria al cliente que su credencial es la que no
    // vale, y no es verdad.
    public static async Task ExigirOperacionDeclaradaAsync(
        HttpContext contexto,
        RequestDelegate siguiente)
    {
        var endpoint = contexto.GetEndpoint();

        // Sin endpoint resuelto no hay ruta que ejecutar. Y las que no son de
        // negocio (OpenApi) se dejan pasar: no tienen operacion de negocio.
        if (endpoint is null || !EsRutaDeNegocio(contexto.Request.Path))
        {
            await siguiente(contexto);
            return;
        }

        if (endpoint.Metadata.GetMetadata<MetadatoDeOperacion>() is null)
        {
            throw new OperacionSinDeclararException(endpoint.DisplayName);
        }

        await siguiente(contexto);
    }

    // Heuristica explicita: las rutas de negocio son las que expone este modulo.
    // Se escribe aqui en vez de inventar un marcador en cada ruta para que el
    // nucleo no dependa de las rutas de OpenApi.
    private static bool EsRutaDeNegocio(PathString ruta) =>
        ruta.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || ruta.Equals("/yo", StringComparison.OrdinalIgnoreCase)
        || ruta.StartsWithSegments("/activar", StringComparison.OrdinalIgnoreCase);

    private static string? LeerToken(HttpContext contexto)
    {
        var cabecera = contexto.Request.Headers.Authorization.ToString();
        const string prefijo = "Bearer ";

        if (!cabecera.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = cabecera[prefijo.Length..].Trim();

        return string.IsNullOrEmpty(token) ? null : token;
    }

    private static IResult SesionNoValida() => AccesoDenegado("Sesion no valida.", esFaltaDeRol: false);

    // El 401 y el 403 salen de aqui y no del caso de uso, porque el filtro corre
    // antes. El cuerpo es el mismo que usaba el endpoint: {"mensaje": "..."}.
    private static IResult AccesoDenegado(string mensaje, bool esFaltaDeRol) =>
        Results.Json(
            new ErrorResponse(mensaje),
            statusCode: esFaltaDeRol
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized);

    // Metadato: dice que operacion es la ruta. Vive en la lista de metadatos del
    // endpoint para que la prueba que recorre las rutas reales del Host lo pueda
    // leer y compararlo con la tabla.
    public sealed class MetadatoDeOperacion(Operacion operacion)
    {
        public Operacion Operacion { get; } = operacion;
    }
}

// Distinta de OperacionSinExigenciaDeRolException a proposito: una es "esta
// operacion no esta en la tabla" y la otra es "esta ruta no dijo que operacion
// es". Las dos son fallos de programacion, no peticiones invalidas del cliente.
public sealed class OperacionSinDeclararException(string ruta)
    : InvalidOperationException($"La ruta {ruta} no declara su operacion en el filtro de acceso.");