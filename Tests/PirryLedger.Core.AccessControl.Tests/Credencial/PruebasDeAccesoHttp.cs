using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// Verifica la frontera HTTP del filtro sin levantar el Host ni PostgreSQL.
// Las pruebas de casos de uso cubren las reglas; estas fijan los estados que
// recibe una petición construida manualmente (RD-06, RD-07 y RD-08).
public sealed class PruebasDeAccesoHttp
{
    private static readonly DateTime Ahora = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UsuarioEstandarRecibe403EnUnaOperacionAdministrativa()
    {
        var escenario = new EscenarioDeSesion(Ahora);
        var token = await escenario.AbrirSesionAsync();
        var resultado = await EjecutarFiltroAsync(
            escenario.Autenticar,
            token,
            Operacion.ListarUsuarios);

        Assert.Equal(StatusCodes.Status403Forbidden, resultado.StatusCode);
    }

    [Fact]
    public async Task PeticionSinCredencialRecibe401AntesDeComprobarElRol()
    {
        var escenario = new EscenarioDeSesion(Ahora);
        var resultado = await EjecutarFiltroAsync(
            escenario.Autenticar,
            string.Empty,
            Operacion.ListarUsuarios);

        Assert.Equal(StatusCodes.Status401Unauthorized, resultado.StatusCode);
    }

    [Fact]
    public async Task OperacionPublicaLlegaAlHandler()
    {
        var escenario = new EscenarioDeSesion(Ahora);
        var contexto = CrearContexto(
            escenario.Autenticar,
            string.Empty,
            Operacion.IniciarSesion);
        var llamada = EndpointFilterInvocationContext.Create(contexto);

        var resultado = await FiltroDeAcceso.AplicarAsync(
            llamada,
            _ => ValueTask.FromResult<object?>("ejecutado"));

        Assert.Equal("ejecutado", resultado);
    }

    private static async Task<HttpResponse> EjecutarFiltroAsync(
        Autenticar autenticar,
        string token,
        Operacion operacion)
    {
        var contexto = CrearContexto(autenticar, token, operacion);
        var llamada = EndpointFilterInvocationContext.Create(contexto);

        var resultado = await FiltroDeAcceso.AplicarAsync(
            llamada,
            _ => ValueTask.FromResult<object?>("no deberia ejecutarse"));

        var respuesta = Assert.IsAssignableFrom<IResult>(resultado);
        await respuesta.ExecuteAsync(contexto);

        return contexto.Response;
    }

    private static DefaultHttpContext CrearContexto(
        Autenticar autenticar,
        string token,
        Operacion operacion)
    {
        var servicios = new ServiceCollection()
            .AddSingleton(autenticar)
            .AddLogging()
            .BuildServiceProvider();
        var contexto = new DefaultHttpContext
        {
            RequestServices = servicios,
        };
        contexto.Request.Headers.Authorization = string.IsNullOrEmpty(token)
            ? string.Empty
            : $"Bearer {token}";
        contexto.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new FiltroDeAcceso.MetadatoDeOperacion(operacion)),
            "prueba"));

        return contexto;
    }
}
