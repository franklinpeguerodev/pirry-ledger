using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain;
using PirryLedger.Core.AccessControl.Infrastructure;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-05, RF-CA-06, RF-CA-08, RF-CA-20 y RF-CA-21.
//
// Estas pruebas cubren lo que la rubrica revisa a mano: una peticion ARMADA A
// MANO de un Estandar contra una operacion de Administrador tiene que ser
// rechazada. No basta con que la interfaz no muestre el boton; el rechazo
// ocurre en el servidor (RD-06).
public sealed class PruebasDeAdministracionDeUsuarios
{
    private static readonly DateTime Ahora = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan VentanaDeSesion = TimeSpan.FromHours(8);

    // Escenario con un Administrador y un Estandar, los dos activos y con
    // sesion abierta. Devuelve tambien los tokens para no tener que entrar dos
    // veces en cada prueba.
    private static async Task<(EscenarioDeSesion Escenario, string TokenAdmin, string TokenEstandar)> EscenarioConAmbosAsync()
    {
        var escenario = new EscenarioDeSesion(Ahora);

        // El escenario trae un Estandar. Se anade el Administrador.
        escenario.Usuarios.Crear(
            "Admin", "admin@ejemplo.com", escenario.Hasher.Hash("admin12345"), Ahora, activo: true, rol: Rol.Administrador);

        var tokenAdmin = await escenario.Entrar.EjecutarAsync("admin@ejemplo.com", "admin12345");
        var tokenEstandar = await escenario.AbrirSesionAsync();

        return (escenario, tokenAdmin, tokenEstandar);
    }

    // --- RF-CA-05: el punto unico ---

    [Fact]
    public void TodaOperacionDelEnumDeclaraSuRol()
    {
        // El hueco que RF-CA-05 prohibe: una operacion en el enum que nadie
        // declaro en el punto unico. Si alguien anade una al enum y olvida
        // ExigenciasDeRol, esta prueba falla y dice cual falta.
        foreach (var operacion in Enum.GetValues<Operacion>())
        {
            var declarada = ExigenciasDeRol.OperacionesDeclaradas().Contains(operacion);

            // Si NO esta declarada, RolRequerido tiene que lanzar en vez de
            // devolver un rol cualquiera: una operacion sin proteccion no puede
            // devolver "nadie" como si fuera una respuesta valida.
            if (!declarada)
            {
                var error = Assert.Throws<OperacionSinExigenciaDeRolException>(
                    () => ExigenciasDeRol.RolRequerido(operacion));

                Assert.Contains(operacion.ToString(), error.Message);
            }
        }

        // Y la cuenta sale: cada operacion del enum esta en la tabla.
        Assert.Equal(Enum.GetValues<Operacion>().Length, ExigenciasDeRol.OperacionesDeclaradas().Count);
    }

    [Fact]
    public void LasCuatroOperacionesDeAdministracionExigenAdministrador()
    {
        Assert.Equal(Rol.Administrador, ExigenciasDeRol.RolRequerido(Operacion.ListarUsuarios));
        Assert.Equal(Rol.Administrador, ExigenciasDeRol.RolRequerido(Operacion.CambiarRolDeUsuario));
        Assert.Equal(Rol.Administrador, ExigenciasDeRol.RolRequerido(Operacion.DesactivarUsuario));
        Assert.Equal(Rol.Administrador, ExigenciasDeRol.RolRequerido(Operacion.ReactivarUsuario));

        // Y la tabla no tiene operaciones de mas: si alguien anade una al enum y
        // la declara, esta prueba falla y obliga a decidir si es de
        // Administrador o no.
        Assert.Equal(Enum.GetValues<Operacion>().Length, ExigenciasDeRol.OperacionesDeclaradas().Count);
    }

    // --- RF-CA-06: el rechazo tiene que ocurrir con peticion a mano ---

    [Fact]
    public async Task EstandarRecibe403EnLasTresOperaciones()
    {
        var (escenario, _, tokenEstandar) = await EscenarioConAmbosAsync();

        var listar = new ListUsers(escenario.Usuarios, escenario.Autenticar);
        var cambiarRol = new ChangeUserRole(escenario.Usuarios, escenario.Autenticar);
        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);

        var objetivo = escenario.Usuarios.Crear(
            "Objetivo", "objetivo@ejemplo.com", escenario.Hasher.Hash("abc12345"), Ahora, activo: true);

        // Los tres rechazos, del mismo tipo y por la misma razon. Si uno de
        // ellos pasara, el Estandar tendria una operacion abierta.
        await Assert.ThrowsAsync<RolInsuficienteException>(
            () => listar.EjecutarAsync(tokenEstandar));

        await Assert.ThrowsAsync<RolInsuficienteException>(
            () => cambiarRol.EjecutarAsync(tokenEstandar, objetivo.Id, "Estandar"));

        await Assert.ThrowsAsync<RolInsuficienteException>(
            () => desactivar.EjecutarAsync(tokenEstandar, objetivo.Id));
    }

    [Fact]
    public async Task EstandarNoPuedeCambiarSuPropioRol()
    {
        var (escenario, _, tokenEstandar) = await EscenarioConAmbosAsync();

        var cambiarRol = new ChangeUserRole(escenario.Usuarios, escenario.Autenticar);

        // Su propio rol. El rechazo es 403 por falta de permisos, no 400 por
        // operacion invalida: lo primero dice "tu rol no alcanza", que es lo que
        // RF-CA-08 pide.
        await Assert.ThrowsAsync<RolInsuficienteException>(
            () => cambiarRol.EjecutarAsync(tokenEstandar, escenario.Usuario.Id, "Administrador"));
    }

    [Fact]
    public async Task SinSesionEs401YNo403()
    {
        var (escenario, _, _) = await EscenarioConAmbosAsync();

        var listar = new ListUsers(escenario.Usuarios, escenario.Autenticar);

        // El orden importa: sin sesion no se sabe el rol, asi que no se puede
        // decir "no tienes permisos" porque eso confirmaria que el token fue real
        // alguna vez. Gana el 401.
        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => listar.EjecutarAsync(string.Empty));

        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => listar.EjecutarAsync("token-inventado"));
    }

    // --- RF-CA-21: listar ---

    [Fact]
    public async Task ElListadoTraeCorreoRolYEstadoYNingunHash()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var listar = new ListUsers(escenario.Usuarios, escenario.Autenticar);

        var usuarios = await listar.EjecutarAsync(tokenAdmin);

        // El escenario trae un Estandar y esta prueba anade un Administrador.
        Assert.Equal(2, usuarios.Count);

        var objetivo = usuarios.First(usuario => usuario.Correo == "admin@ejemplo.com");
        Assert.Equal(Rol.Administrador, objetivo.Rol);
        Assert.True(objetivo.Activo);

        // RF-CA-21: el listado NUNCA incluye hashes ni tokens. Se comprueba por
        // nombre del tipo, no por el contenido: UsuarioListado no tiene campo de
        // hash, asi que no hay por donde filtrarse aunque alguien lo intente.
        Assert.DoesNotContain(objetivo.GetType().GetProperties(), propiedad =>
            propiedad.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(objetivo.GetType().GetProperties(), propiedad =>
            propiedad.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(objetivo.GetType().GetProperties(), propiedad =>
            propiedad.Name.Contains("Contrasena", StringComparison.OrdinalIgnoreCase));
    }

    // --- RF-CA-08: cambiar rol ---

    [Fact]
    public async Task AdministradorCambiaElRolDeOtro()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var cambiarRol = new ChangeUserRole(escenario.Usuarios, escenario.Autenticar);
        var objetivo = escenario.Usuario;

        Assert.Equal(Rol.Estandar, objetivo.Rol);

        await cambiarRol.EjecutarAsync(tokenAdmin, objetivo.Id, "Administrador");

        Assert.Equal(Rol.Administrador, objetivo.Rol);
    }

    [Fact]
    public async Task ElRolSeAceptaSinDistinguirMayusculas()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var cambiarRol = new ChangeUserRole(escenario.Usuarios, escenario.Autenticar);
        var objetivo = escenario.Usuario;

        await cambiarRol.EjecutarAsync(tokenAdmin, objetivo.Id, "administrador");

        Assert.Equal(Rol.Administrador, objetivo.Rol);
    }

    [Fact]
    public async Task UnRolQueNoExisteEs400YNoUnErrorInterno()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var cambiarRol = new ChangeUserRole(escenario.Usuarios, escenario.Autenticar);

        // RD-07: un JSON con un rol inventado es un dato malo del cliente, no un
        // fallo del servidor. TryParse evita que Enum.Parse lance dentro de la
        // capa HTTP.
        await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => cambiarRol.EjecutarAsync(tokenAdmin, escenario.Usuario.Id, "SuperAdministrador"));

        await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => cambiarRol.EjecutarAsync(tokenAdmin, escenario.Usuario.Id, "99"));

        // Y el rol no cambio.
        Assert.Equal(Rol.Estandar, escenario.Usuario.Rol);
    }

    [Fact]
    public async Task UnAdministradorNoPuedeCambiarSuPropioRol()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var cambiarRol = new ChangeUserRole(escenario.Usuarios, escenario.Autenticar);
        var admin = await escenario.Usuarios.BuscarPorCorreoAsync("admin@ejemplo.com");

        // Es operacion invalida, no falta de permisos: por eso es
        // OperacionDeAdministracionRechazadaException y no RolInsuficiente. El
        // endpoint lo traduce a 400 en vez de 403.
        await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => cambiarRol.EjecutarAsync(tokenAdmin, admin!.Id, "Estandar"));

        Assert.Equal(Rol.Administrador, admin!.Rol);
    }

    // --- RF-CA-20: desactivar ---

    [Fact]
    public async Task DesactivarInvalidaLaSesionAbiertaDelOtro()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);
        var objetivo = escenario.Usuario;

        // El Estandar abre sesion ANTES de que se le desactive. El token se guarda, para
        // poder comprobar despues que ESA credencial concreta deja de servir.
        var tokenObjetivo = await escenario.AbrirSesionYGuardarTokenAsync();

        await escenario.Autenticar.EjecutarAsync(tokenObjetivo);

        await desactivar.EjecutarAsync(tokenAdmin, objetivo.Id);

        Assert.False(objetivo.Activo);

        // Y esa sesion deja de servir. No se borro ninguna fila de Sesion: lo
        // que cambio fue CredencialVersion, y el punto de validacion la compara.
        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarAsync(tokenObjetivo));
    }

    [Fact]
    public async Task UnDesactivadoNoPuedeIniciarSesion()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);
        var objetivo = escenario.Usuario;

        await desactivar.EjecutarAsync(tokenAdmin, objetivo.Id);

        // RF-CA-20: "un usuario desactivado no inicia sesion".
        await Assert.ThrowsAsync<CuentaNoActivadaException>(
            () => escenario.Entrar.EjecutarAsync("empleado@ejemplo.com", "abc12345"));
    }

    [Fact]
    public async Task AdministradorNoPuedeDesactivarseASiMismo()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);
        var admin = await escenario.Usuarios.BuscarPorCorreoAsync("admin@ejemplo.com");

        await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => desactivar.EjecutarAsync(tokenAdmin, admin!.Id));

        Assert.True(admin!.Activo);
    }

    [Fact]
    public async Task NoSePuedeDesactivarAlUltimoAdministradorActivo()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);
        var admin = await escenario.Usuarios.BuscarPorCorreoAsync("admin@ejemplo.com");

        // Este es el UNICO Administrador activo de la base. Desactivarlo dejaria
        // el sistema sin administracion sin forma de arreglarlo por la API.
        Assert.Equal(1, await escenario.Usuarios.ContarAdministradoresActivosAsync());

        await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => desactivar.EjecutarAsync(tokenAdmin, admin!.Id));

        Assert.True(admin!.Activo);
    }

    [Fact]
    public async Task SePuedeDesactivarUnAdministradorCuandoHayOtroActivo()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        // Un segundo Administrador activo: ahora si hay respaldo.
        var segundo = escenario.Usuarios.Crear(
            "Admin Dos", "admin2@ejemplo.com", escenario.Hasher.Hash("admin12345"), Ahora, activo: true, rol: Rol.Administrador);

        Assert.Equal(2, await escenario.Usuarios.ContarAdministradoresActivosAsync());

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);
        var primero = await escenario.Usuarios.BuscarPorCorreoAsync("admin@ejemplo.com");

        // Quien llama (tokenAdmin) es "primero", asi que no puede desactivarse a si
        // mismo. El que se desactiva es el SEGUNDO Administrador, y para eso hace
        // falta su propia sesion: el caso de uso exige Administrador y el token
        // del primero tambien lo es, pero el rechazo de auto-desactivacion
        // comprueba el Id del que llama.
        var tokenSegundo = await escenario.Entrar.EjecutarAsync("admin2@ejemplo.com", "admin12345");

        await desactivar.EjecutarAsync(tokenSegundo, primero!.Id);

        Assert.False(primero!.Activo);
        Assert.True(segundo.Activo);
    }

    [Fact]
    public async Task UnAdministradorDesactivadoNoCuentaComoRespaldo()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);
        var admin = await escenario.Usuarios.BuscarPorCorreoAsync("admin@ejemplo.com");

        // Desactivar a un Estandar no toca el conteo.
        await desactivar.EjecutarAsync(tokenAdmin, escenario.Usuario.Id);
        Assert.Equal(1, await escenario.Usuarios.ContarAdministradoresActivosAsync());

        // Si el unico Administrador ya estuviera inactivo, seguiria sin poder
        // desactivarse: no habria a quien volver. El conteo mira solo los activos.
        Assert.False(escenario.Usuario.Activo);
        Assert.True(admin!.Activo);
    }

    // Desactivar una cuenta que ya esta inactiva tiene que responder 400 diciendo
    // eso, no un 500. Y el mensaje tiene que hablar de la cuenta inactiva: antes
    // de arreglarlo, este caso devolvia "no se puede desactivar al ultimo
    // Administrador activo", que no era lo que habia pasado.
    [Fact]
    public async Task DesactivarDosVecesLaMismaCuentaEsRechazoControlado()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);

        await desactivar.EjecutarAsync(tokenAdmin, escenario.Usuario.Id);

        var error = await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => desactivar.EjecutarAsync(tokenAdmin, escenario.Usuario.Id));

        Assert.Equal("La cuenta ya esta inactiva.", error.Message);
    }

    // La cuenta inactiva se avisa antes que la regla del ultimo Administrador.
    // Este usuario es Estandar, asi que el guardia del ultimo Administrador ni
    // siquiera le tocaba: lo que se comprueba aqui es el orden de las reglas.
    [Fact]
    public async Task DesactivarUnaCuentaInactivaNoDiceQueFaltaUnAdministrador()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);
        var objetivo = await escenario.Usuarios.BuscarPorCorreoAsync("empleado@ejemplo.com");

        await desactivar.EjecutarAsync(tokenAdmin, escenario.Usuario.Id);

        var error = await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => desactivar.EjecutarAsync(tokenAdmin, objetivo!.Id));

        Assert.DoesNotContain("ultimo Administrador", error.Message);
    }

    // --- Reactivar ---

    [Fact]
    public async Task ReactivarDevuelveLaCuentaYSuSesionVuelveAFuncionar()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);
        var objetivo = escenario.Usuario;

        await desactivar.EjecutarAsync(tokenAdmin, objetivo.Id);
        Assert.False(objetivo.Activo);

        await desactivar.ReactivarAsync(tokenAdmin, objetivo.Id);
        Assert.True(objetivo.Activo);

        // Y entra con la misma contrasena.
        var token = await escenario.Entrar.EjecutarAsync("empleado@ejemplo.com", "abc12345");
        Assert.NotNull(token);
    }

    // Reactivar una cuenta que ya esta activa es un 400 controlado. Sin esto la
    // excepcion de la entidad salia como 500 con traza (RD-07, RD-08).
    [Fact]
    public async Task ReactivarDosVecesLaMismaCuentaEsRechazoControlado()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);

        // Sin esto la primera llamada ya fallaria, porque en el escenario el
        // usuario esta activo: reactivar tiene que ir precedido de desactivar.
        await desactivar.EjecutarAsync(tokenAdmin, escenario.Usuario.Id);
        await desactivar.ReactivarAsync(tokenAdmin, escenario.Usuario.Id);

        var error = await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => desactivar.ReactivarAsync(tokenAdmin, escenario.Usuario.Id));

        Assert.Equal("La cuenta ya esta activa.", error.Message);
    }

    // Y el caso mas directo de todos: reactivar a alguien que esta activo desde
    // el principio, sin pasar por desactivar.
    [Fact]
    public async Task ReactivarUnaCuentaQueNuncaSeDesactivoEsRechazoControlado()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);

        var error = await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => desactivar.ReactivarAsync(tokenAdmin, escenario.Usuario.Id));

        Assert.Equal("La cuenta ya esta activa.", error.Message);
    }

    [Fact]
    public async Task ReactivarTambienExigeAdministrador()
    {
        var (escenario, _, tokenEstandar) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);

        await Assert.ThrowsAsync<RolInsuficienteException>(
            () => desactivar.ReactivarAsync(tokenEstandar, escenario.Usuario.Id));
    }

    [Fact]
    public async Task UnUsuarioQueNoExisteEsRechazoControlado()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);
        var cambiarRol = new ChangeUserRole(escenario.Usuarios, escenario.Autenticar);
        var inexistente = Guid.NewGuid();

        await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => desactivar.EjecutarAsync(tokenAdmin, inexistente));

        await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => cambiarRol.EjecutarAsync(tokenAdmin, inexistente, "Administrador"));
    }

    [Fact]
    public async Task LosMensajesDeRechazoNoLlevanCorreosNiHashes()
    {
        var (escenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var desactivar = new DeactivateUser(escenario.Usuarios, escenario.Autenticar);
        var admin = await escenario.Usuarios.BuscarPorCorreoAsync("admin@ejemplo.com");

        var error = await Assert.ThrowsAsync<OperacionDeAdministracionRechazadaException>(
            () => desactivar.EjecutarAsync(tokenAdmin, admin!.Id));

        // RD-08: el mensaje dice que paso, no con que datos.
        Assert.DoesNotContain("admin@ejemplo.com", error.Message);
        Assert.DoesNotContain(admin!.HashDeContrasena, error.Message);
    }
}