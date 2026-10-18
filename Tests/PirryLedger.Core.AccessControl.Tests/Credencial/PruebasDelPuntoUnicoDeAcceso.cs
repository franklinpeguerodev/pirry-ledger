using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-05: "existe un punto del Code donde se puede leer la Requirement de Role
// de cada Operation". El criterio tiene una parte que es facil de cumplir de
// mentira: basta con escribir una tabla bonita y dejar fuera la mitad de las
// rutas. Estas pruebas lo evitan.
//
// La diferencia con una lista escrita a mano es toda: estas pruebas leen las
// rutas de verdad que produce MapAccessControl. Si alguien anade un MapGet nuevo y
// no lo declara, esto falla sin que nadie tenga que acordarse de updating una
// lista.
public class PruebasDelPuntoUnicoDeAcceso
{
    // Los tipos que los handlers piden por inyeccion. Se sustituyen por
    // instancias sin construir porque esta prueba NUNCA ejecuta un endpoint: solo
    // lee metadatos. Aun asi hay que registrarlos, porque Minimal API valida los
    // parametros al construir la Route y falla si no encuentra el servicio.
    private static readonly Type[] ServiciosDeLasRutas =
    [
        typeof(RegisterUser),
        typeof(ActivateAccount),
        typeof(ResendActivationLink),
        typeof(Login),
        typeof(Logout),
        typeof(Authenticate),
        typeof(ListUsers),
        typeof(ChangeUserRole),
        typeof(DeactivateUser),
        typeof(PirryLedger.Core.Contracts.Time.IClock),
    ];

    // Las rutas reales, leidas de la aplicacion construida. No hay ningun array de
    // cadenas en esta clase.
    private static IReadOnlyList<RouteEndpoint> RutasReales()
    {
        var builder = WebApplication.CreateBuilder();

        foreach (var Type in ServiciosDeLasRutas)
        {
            builder.Services.AddSingleton(
                Type,
                _ => System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(Type));
        }

        var app = builder.Build();

        app.MapAccessControl();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(fuente => fuente.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(Route => !string.IsNullOrEmpty(Route.RoutePattern.RawText))
            .ToList();
    }

    // Ninguna Route se queda sin declarar. Una Route sin WithAccess no tiene
    // metadato, y ademas el middleware la rechazaria en ejecucion: esta prueba es
    // la que avisa en la compilacion.
    [Fact]
    public void TodaRutaDeclaraSuOperacion()
    {
        var sinDeclarar = RutasReales()
            .Where(Route => Route.Metadata.GetMetadata<AccessFilter.OperationMetadata>() is null)
            .Select(Route => Route.RoutePattern.RawText!)
            .ToList();

        Assert.True(
            sinDeclarar.Count == 0,
            "Estas rutas no declaran su Operation en RoleRequirements: "
            + string.Join(", ", sinDeclarar));
    }

    // Y al reves: cada entrada de la tabla tiene que tener una Route. Una entrada
    // sin Route es una Operation que nadie puede ejecutar, que es Code muerto
    // disfrazado de requisito.
    [Fact]
    public void TodaOperacionDeLaTablaTieneUnaRuta()
    {
        var declaradas = RutasReales()
            .Select(Route => Route.Metadata.GetMetadata<AccessFilter.OperationMetadata>()?.Operation)
            .Where(Operation => Operation is not null)
            .ToHashSet();

        var sinRuta = RoleRequirements.DeclaredOperations()
            .Where(Operation => !declaradas.Contains(Operation))
            .ToList();

        Assert.True(
            sinRuta.Count == 0,
            "Estas operaciones estan en la tabla pero ninguna Route las usa: "
            + string.Join(", ", sinRuta));
    }

    // Y el tercer lado: cada Operation del enum tiene que estar en la tabla.
    // Estos tres juntos cierran el circulo enum <-> tabla <-> rutas, sin que
    // sobre nada ni falte nada.
    [Fact]
    public void CadaOperacionDelEnumEstaEnLaTabla()
    {
        foreach (var Operation in Enum.GetValues<Operation>())
        {
            // Que no lance es la propia prueba: RequiredLevel lanza
            // OperationWithoutRoleRequirementException si falta la entrada.
            var level = RoleRequirements.RequiredLevel(Operation);

            Assert.True(
                Enum.IsDefined(level),
                $"{Operation} devolvio un nivel que no existe: {level}");
        }
    }

    // Los tres niveles tienen que existir de verdad. Una tabla que solo usara
    // Administrator y Public tendria un nivel muerto, y uno que solo usara
    // Administrator no estaria cumpliendo el criterio.
    [Fact]
    public void LaTablaUsaLosTresNiveles()
    {
        var niveles = RoleRequirements.DeclaredOperations()
            .Select(RoleRequirements.RequiredLevel)
            .ToHashSet();

        Assert.Contains(AccessLevel.Public, niveles);
        Assert.Contains(AccessLevel.AnySession, niveles);
        Assert.Contains(AccessLevel.Administrator, niveles);
    }

    // Pedir el Role de una Operation publica es un error de programacion, y tiene
    // que notarse. Si devolviera un Role cualquiera, un endpoint Public podria
    // acabar exigiendo Administrator sin que nadie se entere.
    [Fact]
    public void PedirElRolDeUnaOperacionPublicaLanza()
    {
        Assert.Throws<ArgumentException>(
            () => RoleRequirements.RequiredRole(Operation.Login));
    }

    // Close Session es de "cualquier Session" pero no llega a comprobar que la
    // Session siga viva. Aqui no se puede probar el 204 con token caducado porque
    // eso vive en el endpoint y necesita la aplicacion entera; lo que se fija
    // aqui es que el nivel declarado es el que dice la nota de RoleRequirements.
    [Fact]
    public void CerrarSesionEsDeCualquierSesionYNoDeAdministrador()
    {
        Assert.Equal(
            AccessLevel.AnySession,
            RoleRequirements.RequiredLevel(Operation.Logout));
    }

    // Y que tolera la credencial invalida. Esta es la propiedad que evita la
    // regresion: sin ella, el filtro exigia una Session valida y logout devolvia
    // 401 sin token y 500 con un token corrupto. Comprobado contra la app real.
    [Fact]
    public void CerrarSesionToleraUnaCredencialInvalida()
    {
        Assert.True(RoleRequirements.ToleratesInvalidCredential(Operation.Logout));
    }

    // Ninguna otra Operation puede declararse tolerante. Si se colara una, el
    // filtro dejaria de comprobar su Session en silencio.
    [Fact]
    public void SoloCerrarSesionToleraLaCredencialInvalida()
    {
        var tolerantes = RoleRequirements.DeclaredOperations()
            .Where(RoleRequirements.ToleratesInvalidCredential)
            .ToList();

        Assert.Equal([Operation.Logout], tolerantes);
    }

    // Y en particular /yo NO puede tolerarla: ahi una credencial invalida es un
    // 401 de verdad, no un 204.
    [Fact]
    public void ConsultarSesionPropiaNoToleraLaCredencialInvalida()
    {
        Assert.False(
            RoleRequirements.ToleratesInvalidCredential(Operation.QueryOwnSession));
    }

    // Las tres de administracion tienen que exigir el Role concreto, y ese Role se
    // pide a la tabla, no se escribe en el endpoint.
    [Theory]
    [InlineData(Operation.ListUsers)]
    [InlineData(Operation.ChangeUserRole)]
    [InlineData(Operation.DeactivateUser)]
    [InlineData(Operation.ReactivateUser)]
    public void LasOperacionesDeAdministracionExigenElRolDeAdministrador(Operation operation)
    {
        Assert.Equal(AccessLevel.Administrator, RoleRequirements.RequiredLevel(operation));
        Assert.Equal(Role.Administrator, RoleRequirements.RequiredRole(operation));
    }

    // El caso que mas se repite al releer RF-CA-05: las cinco rutas de
    // administracion declaradas, contadas desde las rutas reales. Si alguien
    // anade una quinta y la deja fuera de administracion, esto salta.
    [Fact]
    public void HayExactamenteCincoRutasDeAdministrador()
    {
        var deAdministracion = RutasReales()
            .Where(Route => Route.RoutePattern.RawText!.StartsWith("/api/admin"))
            .ToList();

        Assert.Equal(5, deAdministracion.Count);

        foreach (var Route in deAdministracion)
        {
            Assert.Equal(
                AccessLevel.Administrator,
                RoleRequirements.RequiredLevel(
                    Route.Metadata.GetMetadata<AccessFilter.OperationMetadata>()!.Operation));
        }
    }
}