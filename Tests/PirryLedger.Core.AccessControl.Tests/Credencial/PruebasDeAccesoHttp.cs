using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// Verifica la frontera HTTP del filtro sin levantar el Host ni PostgreSQL.
// Las pruebas de casos de uso cubren las reglas; estas fijan los estados que
// recibe una petición construida manualmente (RD-06, RD-07 y RD-08).
public sealed class PruebasDeAccesoHttp
{
    private static readonly DateTime utcNow = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UsuarioEstandarRecibe403EnUnaOperacionAdministrativa()
    {
        var scenario = new SessionScenario(utcNow);
        var token = await scenario.OpenSessionAsync();
        var result = await EjecutarFiltroAsync(
            scenario.Authenticate,
            token,
            Operation.ListUsers);

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task PeticionSinCredencialRecibe401AntesDeComprobarElRol()
    {
        var scenario = new SessionScenario(utcNow);
        var result = await EjecutarFiltroAsync(
            scenario.Authenticate,
            string.Empty,
            Operation.ListUsers);

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task OperacionPublicaLlegaAlHandler()
    {
        var scenario = new SessionScenario(utcNow);
        var context = CrearContexto(
            scenario.Authenticate,
            string.Empty,
            Operation.Login);
        var llamada = EndpointFilterInvocationContext.Create(context);

        var result = await AccessFilter.ApplyAsync(
            llamada,
            _ => ValueTask.FromResult<object?>("ejecutado"));

        Assert.Equal("ejecutado", result);
    }

    private static async Task<HttpResponse> EjecutarFiltroAsync(
        Authenticate authenticate,
        string token,
        Operation operation)
    {
        var context = CrearContexto(authenticate, token, operation);
        var llamada = EndpointFilterInvocationContext.Create(context);

        var result = await AccessFilter.ApplyAsync(
            llamada,
            _ => ValueTask.FromResult<object?>("no deberia ejecutarse"));

        var response = Assert.IsAssignableFrom<IResult>(result);
        await response.ExecuteAsync(context);

        return context.Response;
    }

    private static DefaultHttpContext CrearContexto(
        Authenticate authenticate,
        string token,
        Operation operation)
    {
        var servicios = new ServiceCollection()
            .AddSingleton(authenticate)
            .AddLogging()
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = servicios,
        };
        context.Request.Headers.Authorization = string.IsNullOrEmpty(token)
            ? string.Empty
            : $"Bearer {token}";
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AccessFilter.OperationMetadata(operation)),
            "prueba"));

        return context;
    }
}
