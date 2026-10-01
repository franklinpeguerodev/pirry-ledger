using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-05: "existe un punto del codigo donde se puede leer la exigencia de rol
// de cada operacion". El criterio tiene una parte que es facil de cumplir de
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
    // parametros al construir la ruta y falla si no encuentra el servicio.
    private static readonly Type[] ServiciosDeLasRutas =
    [
        typeof(RegisterUser),
        typeof(ActivateAccount),
        typeof(ResendActivationLink),
        typeof(Login),
        typeof(Logout),
        typeof(Autenticar),
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

        foreach (var tipo in ServiciosDeLasRutas)
        {
            builder.Services.AddSingleton(
                tipo,
                _ => System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(tipo));
        }

        var app = builder.Build();

        app.MapAccessControl();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(fuente => fuente.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(ruta => !string.IsNullOrEmpty(ruta.RoutePattern.RawText))
            .ToList();
    }

    // Ninguna ruta se queda sin declarar. Una ruta sin ConAcceso no tiene
    // metadato, y ademas el middleware la rechazaria en ejecucion: esta prueba es
    // la que avisa en la compilacion.
    [Fact]
    public void TodaRutaDeclaraSuOperacion()
    {
        var sinDeclarar = RutasReales()
            .Where(ruta => ruta.Metadata.GetMetadata<FiltroDeAcceso.MetadatoDeOperacion>() is null)
            .Select(ruta => ruta.RoutePattern.RawText!)
            .ToList();

        Assert.True(
            sinDeclarar.Count == 0,
            "Estas rutas no declaran su operacion en ExigenciasDeRol: "
            + string.Join(", ", sinDeclarar));
    }

    // Y al reves: cada entrada de la tabla tiene que tener una ruta. Una entrada
    // sin ruta es una operacion que nadie puede ejecutar, que es codigo muerto
    // disfrazado de requisito.
    [Fact]
    public void TodaOperacionDeLaTablaTieneUnaRuta()
    {
        var declaradas = RutasReales()
            .Select(ruta => ruta.Metadata.GetMetadata<FiltroDeAcceso.MetadatoDeOperacion>()?.Operacion)
            .Where(operacion => operacion is not null)
            .ToHashSet();

        var sinRuta = ExigenciasDeRol.OperacionesDeclaradas()
            .Where(operacion => !declaradas.Contains(operacion))
            .ToList();

        Assert.True(
            sinRuta.Count == 0,
            "Estas operaciones estan en la tabla pero ninguna ruta las usa: "
            + string.Join(", ", sinRuta));
    }

    // Y el tercer lado: cada operacion del enum tiene que estar en la tabla.
    // Estos tres juntos cierran el circulo enum <-> tabla <-> rutas, sin que
    // sobre nada ni falte nada.
    [Fact]
    public void CadaOperacionDelEnumEstaEnLaTabla()
    {
        foreach (var operacion in Enum.GetValues<Operacion>())
        {
            // Que no lance es la propia prueba: NivelRequerido lanza
            // OperacionSinExigenciaDeRolException si falta la entrada.
            var nivel = ExigenciasDeRol.NivelRequerido(operacion);

            Assert.True(
                Enum.IsDefined(nivel),
                $"{operacion} devolvio un nivel que no existe: {nivel}");
        }
    }

    // Los tres niveles tienen que existir de verdad. Una tabla que solo usara
    // Administrador y Publico tendria un nivel muerto, y uno que solo usara
    // Administrador no estaria cumpliendo el criterio.
    [Fact]
    public void LaTablaUsaLosTresNiveles()
    {
        var niveles = ExigenciasDeRol.OperacionesDeclaradas()
            .Select(ExigenciasDeRol.NivelRequerido)
            .ToHashSet();

        Assert.Contains(NivelAcceso.Publico, niveles);
        Assert.Contains(NivelAcceso.CualquierSesion, niveles);
        Assert.Contains(NivelAcceso.Administrador, niveles);
    }

    // Pedir el rol de una operacion publica es un error de programacion, y tiene
    // que notarse. Si devolviera un rol cualquiera, un endpoint publico podria
    // acabar exigiendo Administrador sin que nadie se entere.
    [Fact]
    public void PedirElRolDeUnaOperacionPublicaLanza()
    {
        Assert.Throws<ArgumentException>(
            () => ExigenciasDeRol.RolRequerido(Operacion.IniciarSesion));
    }

    // Cerrar sesion es de "cualquier sesion" pero no llega a comprobar que la
    // sesion siga viva. Aqui no se puede probar el 204 con token caducado porque
    // eso vive en el endpoint y necesita la aplicacion entera; lo que se fija
    // aqui es que el nivel declarado es el que dice la nota de ExigenciasDeRol.
    [Fact]
    public void CerrarSesionEsDeCualquierSesionYNoDeAdministrador()
    {
        Assert.Equal(
            NivelAcceso.CualquierSesion,
            ExigenciasDeRol.NivelRequerido(Operacion.CerrarSesion));
    }

    // Y que tolera la credencial invalida. Esta es la propiedad que evita la
    // regresion: sin ella, el filtro exigia una sesion valida y logout devolvia
    // 401 sin token y 500 con un token corrupto. Comprobado contra la app real.
    [Fact]
    public void CerrarSesionToleraUnaCredencialInvalida()
    {
        Assert.True(ExigenciasDeRol.ToleraCredencialInvalida(Operacion.CerrarSesion));
    }

    // Ninguna otra operacion puede declararse tolerante. Si se colara una, el
    // filtro dejaria de comprobar su sesion en silencio.
    [Fact]
    public void SoloCerrarSesionToleraLaCredencialInvalida()
    {
        var tolerantes = ExigenciasDeRol.OperacionesDeclaradas()
            .Where(ExigenciasDeRol.ToleraCredencialInvalida)
            .ToList();

        Assert.Equal([Operacion.CerrarSesion], tolerantes);
    }

    // Y en particular /yo NO puede tolerarla: ahi una credencial invalida es un
    // 401 de verdad, no un 204.
    [Fact]
    public void ConsultarSesionPropiaNoToleraLaCredencialInvalida()
    {
        Assert.False(
            ExigenciasDeRol.ToleraCredencialInvalida(Operacion.ConsultarSesionPropia));
    }

    // Las tres de administracion tienen que exigir el rol concreto, y ese rol se
    // pide a la tabla, no se escribe en el endpoint.
    [Theory]
    [InlineData(Operacion.ListarUsuarios)]
    [InlineData(Operacion.CambiarRolDeUsuario)]
    [InlineData(Operacion.DesactivarUsuario)]
    [InlineData(Operacion.ReactivarUsuario)]
    public void LasOperacionesDeAdministracionExigenElRolDeAdministrador(Operacion operacion)
    {
        Assert.Equal(NivelAcceso.Administrador, ExigenciasDeRol.NivelRequerido(operacion));
        Assert.Equal(Domain.Rol.Administrador, ExigenciasDeRol.RolRequerido(operacion));
    }

    // El caso que mas se repite al releer RF-CA-05: las cuatro rutas de
    // administracion declaradas, contadas desde las rutas reales. Si alguien
    // anade una quinta y la deja fuera de administracion, esto salta.
    [Fact]
    public void HayExactamenteCuatroRutasDeAdministrador()
    {
        var deAdministracion = RutasReales()
            .Where(ruta => ruta.RoutePattern.RawText!.StartsWith("/api/admin"))
            .ToList();

        Assert.Equal(4, deAdministracion.Count);

        foreach (var ruta in deAdministracion)
        {
            Assert.Equal(
                NivelAcceso.Administrador,
                ExigenciasDeRol.NivelRequerido(
                    ruta.Metadata.GetMetadata<FiltroDeAcceso.MetadatoDeOperacion>()!.Operacion));
        }
    }
}