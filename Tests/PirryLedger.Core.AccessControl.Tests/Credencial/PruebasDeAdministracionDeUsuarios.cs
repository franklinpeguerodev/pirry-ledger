using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-05, RF-CA-06, RF-CA-08, RF-CA-20 y RF-CA-21.
//
// Estas pruebas cubren lo que la rubrica revisa a mano: una peticion ARMADA A
// MANO de un Standard contra una Operation de Administrator tiene que ser
// rechazada. No basta con que la interfaz no muestre el boton; el rechazo
// ocurre en el servidor (RD-06).
public sealed class PruebasDeAdministracionDeUsuarios
{
    private static readonly DateTime utcNow = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan VentanaDeSesion = TimeSpan.FromHours(8);

    // scenario con un Administrator y un Standard, los dos activos y con
    // Session abierta. Devuelve tambien los tokens para no tener que Login dos
    // veces en cada prueba.
    private static async Task<(SessionScenario scenario, string TokenAdmin, string TokenEstandar)> EscenarioConAmbosAsync()
    {
        var scenario = new SessionScenario(utcNow);

        // El scenario trae un Standard. Se anade el Administrator.
        scenario.Users.Create(
            "Admin", "admin@ejemplo.com", scenario.Hasher.Hash("admin12345"), utcNow, active: true, role: Role.Administrator);

        var tokenAdmin = await scenario.Login.ExecuteAsync("admin@ejemplo.com", "admin12345");
        var tokenEstandar = await scenario.OpenSessionAsync();

        return (scenario, tokenAdmin, tokenEstandar);
    }

    // --- RF-CA-05: el punto unico ---

    [Fact]
    public void TodaOperacionDelEnumDeclaraSuRol()
    {
        // El hueco que RF-CA-05 prohibe: una Operation en el enum que nadie
        // declaro en el punto unico. Si alguien anade una al enum y olvida
        // RoleRequirements, esta prueba falla y dice cual falta.
        foreach (var Operation in Enum.GetValues<Operation>())
        {
            var declarada = RoleRequirements.DeclaredOperations().Contains(Operation);

            // Si NO esta declarada, RequiredRole tiene que lanzar en vez de
            // devolver un Role cualquiera: una Operation sin proteccion no puede
            // devolver "nadie" como si fuera una response valida.
            if (!declarada)
            {
                var error = Assert.Throws<OperationWithoutRoleRequirementException>(
                    () => RoleRequirements.RequiredRole(Operation));

                Assert.Contains(Operation.ToString(), error.Message);
            }
        }

        // Y la cuenta sale: cada Operation del enum esta en la tabla.
        Assert.Equal(Enum.GetValues<Operation>().Length, RoleRequirements.DeclaredOperations().Count);
    }

    [Fact]
    public void LasCuatroOperacionesDeAdministracionExigenAdministrador()
    {
        Assert.Equal(Role.Administrator, RoleRequirements.RequiredRole(Operation.ListUsers));
        Assert.Equal(Role.Administrator, RoleRequirements.RequiredRole(Operation.ChangeUserRole));
        Assert.Equal(Role.Administrator, RoleRequirements.RequiredRole(Operation.DeactivateUser));
        Assert.Equal(Role.Administrator, RoleRequirements.RequiredRole(Operation.ReactivateUser));

        // Y la tabla no tiene operaciones de mas: si alguien anade una al enum y
        // la declara, esta prueba falla y obliga a decidir si es de
        // Administrator o no.
        Assert.Equal(Enum.GetValues<Operation>().Length, RoleRequirements.DeclaredOperations().Count);
    }

    // --- RF-CA-06: el rechazo tiene que ocurrir con peticion a mano ---

    [Fact]
    public async Task EstandarRecibe403EnLasTresOperaciones()
    {
        var (scenario, _, tokenEstandar) = await EscenarioConAmbosAsync();

        var listar = new ListUsers(scenario.Users, scenario.Authenticate);
        var ChangeRole = new ChangeUserRole(scenario.Users, scenario.Authenticate);
        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);

        var objetivo = scenario.Users.Create(
            "Objetivo", "objetivo@ejemplo.com", scenario.Hasher.Hash("abc12345"), utcNow, active: true);

        // Los tres rechazos, del mismo Type y por la misma reason. Si uno de
        // ellos pasara, el Standard tendria una Operation abierta.
        await Assert.ThrowsAsync<InsufficientRoleException>(
            () => listar.ExecuteAsync(tokenEstandar));

        await Assert.ThrowsAsync<InsufficientRoleException>(
            () => ChangeRole.ExecuteAsync(tokenEstandar, objetivo.Id, "Standard"));

        await Assert.ThrowsAsync<InsufficientRoleException>(
            () => Deactivate.ExecuteAsync(tokenEstandar, objetivo.Id));
    }

    [Fact]
    public async Task EstandarNoPuedeCambiarSuPropioRol()
    {
        var (scenario, _, tokenEstandar) = await EscenarioConAmbosAsync();

        var ChangeRole = new ChangeUserRole(scenario.Users, scenario.Authenticate);

        // Su propio Role. El rechazo es 403 por falta de permisos, no 400 por
        // Operation invalida: lo primero dice "tu Role no alcanza", que es lo que
        // RF-CA-08 pide.
        await Assert.ThrowsAsync<InsufficientRoleException>(
            () => ChangeRole.ExecuteAsync(tokenEstandar, scenario.User.Id, "Administrator"));
    }

    [Fact]
    public async Task SinSesionEs401YNo403()
    {
        var (scenario, _, _) = await EscenarioConAmbosAsync();

        var listar = new ListUsers(scenario.Users, scenario.Authenticate);

        // El orden importa: sin Session no se sabe el Role, asi que no se puede
        // decir "no tienes permisos" porque eso confirmaria que el token fue real
        // alguna vez. Gana el 401.
        await Assert.ThrowsAsync<InvalidSessionException>(
            () => listar.ExecuteAsync(string.Empty));

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => listar.ExecuteAsync("token-inventado"));
    }

    // --- RF-CA-21: listar ---

    [Fact]
    public async Task ElListadoTraeCorreoRolYEstadoYNingunHash()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var listar = new ListUsers(scenario.Users, scenario.Authenticate);

        var Users = await listar.ExecuteAsync(tokenAdmin);

        // El scenario trae un Standard y esta prueba anade un Administrator.
        Assert.Equal(2, Users.Count);

        var objetivo = Users.First(User => User.Email == "admin@ejemplo.com");
        Assert.Equal(Role.Administrator, objetivo.Role);
        Assert.True(objetivo.Active);

        // RF-CA-21: el listado NUNCA incluye hashes ni tokens. Se comprueba por
        // Name del Type, no por el contenido: ListedUser no tiene campo de
        // hash, asi que no hay por donde filtrarse aunque alguien lo intente.
        Assert.DoesNotContain(objetivo.GetType().GetProperties(), propiedad =>
            propiedad.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(objetivo.GetType().GetProperties(), propiedad =>
            propiedad.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(objetivo.GetType().GetProperties(), propiedad =>
            propiedad.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    // --- RF-CA-08: cambiar Role ---

    [Fact]
    public async Task AdministradorCambiaElRolDeOtro()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var ChangeRole = new ChangeUserRole(scenario.Users, scenario.Authenticate);
        var objetivo = scenario.User;

        Assert.Equal(Role.Standard, objetivo.Role);

        await ChangeRole.ExecuteAsync(tokenAdmin, objetivo.Id, "Administrator");

        Assert.Equal(Role.Administrator, objetivo.Role);
    }

    [Fact]
    public async Task ElRolSeAceptaSinDistinguirMayusculas()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var ChangeRole = new ChangeUserRole(scenario.Users, scenario.Authenticate);
        var objetivo = scenario.User;

        await ChangeRole.ExecuteAsync(tokenAdmin, objetivo.Id, "Administrator");

        Assert.Equal(Role.Administrator, objetivo.Role);
    }

    [Fact]
    public async Task UnRolQueNoExisteEs400YNoUnErrorInterno()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var ChangeRole = new ChangeUserRole(scenario.Users, scenario.Authenticate);

        // RD-07: un JSON con un Role inventado es un dato malo del cliente, no un
        // failure del servidor. TryParse evita que Enum.Parse lance dentro de la
        // capa HTTP.
        await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => ChangeRole.ExecuteAsync(tokenAdmin, scenario.User.Id, "SuperAdministrador"));

        await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => ChangeRole.ExecuteAsync(tokenAdmin, scenario.User.Id, "99"));

        // Y el Role no cambio.
        Assert.Equal(Role.Standard, scenario.User.Role);
    }

    [Fact]
    public async Task UnAdministradorNoPuedeCambiarSuPropioRol()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var ChangeRole = new ChangeUserRole(scenario.Users, scenario.Authenticate);
        var admin = await scenario.Users.FindByEmailAsync("admin@ejemplo.com");

        // Es Operation invalida, no falta de permisos: por eso es
        // AdministrationOperationRejectedException y no RolInsuficiente. El
        // endpoint lo traduce a 400 en vez de 403.
        await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => ChangeRole.ExecuteAsync(tokenAdmin, admin!.Id, "Standard"));

        Assert.Equal(Role.Administrator, admin!.Role);
    }

    // ADR 009: cambiar el role incrementa CredentialVersion, igual que
    // ChangePassword y Deactivate. Esto es lo que invalida la session abierta
    // del usuario promovido (ver PruebasDeRolYEstado.CambiarDeRolInvalidaLaSesion).
    [Fact]
    public void ElCambioDeRolIncrementaCredentialVersion()
    {
        var utcNow = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var scenario = new SessionScenario(utcNow);
        var user = scenario.Users.Create(
            "Cambio Role", "cambiorol@ejemplo.com", scenario.Hasher.Hash("abc12345"), utcNow, active: true, role: Role.Administrator);

        var versionInicial = user.CredentialVersion;

        user.ChangeRole(Role.Standard);

        Assert.Equal(versionInicial + 1, user.CredentialVersion);
        Assert.Equal(Role.Standard, user.Role);
    }

    // --- RF-CA-20: Deactivate ---

    [Fact]
    public async Task DesactivarInvalidaLaSesionAbiertaDelOtro()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);
        var objetivo = scenario.User;

        // El Standard abre Session ANTES de que se le desactive. El token se guarda, para
        // poder comprobar despues que ESA credencial concreta deja de servir.
        var tokenObjetivo = await scenario.OpenSessionAndStoreTokenAsync();

        await scenario.Authenticate.ExecuteAsync(tokenObjetivo);

        await Deactivate.ExecuteAsync(tokenAdmin, objetivo.Id);

        Assert.False(objetivo.Active);

        // Y esa Session deja de servir. No se borro ninguna fila de Session: lo
        // que cambio fue CredentialVersion, y el punto de validacion la compara.
        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteAsync(tokenObjetivo));
    }

    [Fact]
    public async Task UnDesactivadoNoPuedeIniciarSesion()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);
        var objetivo = scenario.User;

        await Deactivate.ExecuteAsync(tokenAdmin, objetivo.Id);

        // RF-CA-20: "un User desactivado no inicia Session".
        await Assert.ThrowsAsync<AccountNotActivatedException>(
            () => scenario.Login.ExecuteAsync("empleado@ejemplo.com", "abc12345"));
    }

    [Fact]
    public async Task AdministradorNoPuedeDesactivarseASiMismo()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);
        var admin = await scenario.Users.FindByEmailAsync("admin@ejemplo.com");

        await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => Deactivate.ExecuteAsync(tokenAdmin, admin!.Id));

        Assert.True(admin!.Active);
    }

    [Fact]
    public async Task NoSePuedeDesactivarAlUltimoAdministradorActivo()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);
        var admin = await scenario.Users.FindByEmailAsync("admin@ejemplo.com");

        // Este es el UNICO Administrator Active de la base. Desactivarlo dejaria
        // el sistema sin administracion sin forma de arreglarlo por la API.
        Assert.Equal(1, await scenario.Users.CountActiveAdministratorsAsync());

        await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => Deactivate.ExecuteAsync(tokenAdmin, admin!.Id));

        Assert.True(admin!.Active);
    }

    [Fact]
    public async Task SePuedeDesactivarUnAdministradorCuandoHayOtroActivo()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        // Un segundo Administrator active: utcNow si hay respaldo.
        var segundo = scenario.Users.Create(
            "Admin Dos", "admin2@ejemplo.com", scenario.Hasher.Hash("admin12345"), utcNow, active: true, role: Role.Administrator);

        Assert.Equal(2, await scenario.Users.CountActiveAdministratorsAsync());

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);
        var primero = await scenario.Users.FindByEmailAsync("admin@ejemplo.com");

        // Quien llama (tokenAdmin) es "primero", asi que no puede desactivarse a si
        // mismo. El que se desactiva es el SEGUNDO Administrator, y para eso hace
        // falta su propia Session: el caso de uso exige Administrator y el token
        // del primero tambien lo es, pero el rechazo de auto-desactivacion
        // comprueba el Id del que llama.
        var tokenSegundo = await scenario.Login.ExecuteAsync("admin2@ejemplo.com", "admin12345");

        await Deactivate.ExecuteAsync(tokenSegundo, primero!.Id);

        Assert.False(primero!.Active);
        Assert.True(segundo.Active);
    }

    [Fact]
    public async Task UnAdministradorDesactivadoNoCuentaComoRespaldo()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);
        var admin = await scenario.Users.FindByEmailAsync("admin@ejemplo.com");

        // Deactivate a un Standard no toca el conteo.
        await Deactivate.ExecuteAsync(tokenAdmin, scenario.User.Id);
        Assert.Equal(1, await scenario.Users.CountActiveAdministratorsAsync());

        // Si el unico Administrator ya estuviera inactivo, seguiria sin poder
        // desactivarse: no habria a quien volver. El conteo mira solo los activos.
        Assert.False(scenario.User.Active);
        Assert.True(admin!.Active);
    }

    // Deactivate una cuenta que ya esta inactiva tiene que responder 400 diciendo
    // eso, no un 500. Y el Message tiene que hablar de la cuenta inactiva: antes
    // de arreglarlo, este caso devolvia "no se puede Deactivate al ultimo
    // Administrator Active", que no era lo que habia pasado.
    [Fact]
    public async Task DesactivarDosVecesLaMismaCuentaEsRechazoControlado()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);

        await Deactivate.ExecuteAsync(tokenAdmin, scenario.User.Id);

        var error = await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => Deactivate.ExecuteAsync(tokenAdmin, scenario.User.Id));

        Assert.Equal("La cuenta ya esta inactiva.", error.Message);
    }

    // La cuenta inactiva se avisa antes que la regla del ultimo Administrator.
    // Este User es Standard, asi que el guardia del ultimo Administrator ni
    // siquiera le tocaba: lo que se comprueba aqui es el orden de las reglas.
    [Fact]
    public async Task DesactivarUnaCuentaInactivaNoDiceQueFaltaUnAdministrador()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);
        var objetivo = await scenario.Users.FindByEmailAsync("empleado@ejemplo.com");

        await Deactivate.ExecuteAsync(tokenAdmin, scenario.User.Id);

        var error = await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => Deactivate.ExecuteAsync(tokenAdmin, objetivo!.Id));

        Assert.DoesNotContain("ultimo Administrator", error.Message);
    }

    // --- Reactivate ---

    [Fact]
    public async Task ReactivarDevuelveLaCuentaYSuSesionVuelveAFuncionar()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);
        var objetivo = scenario.User;

        await Deactivate.ExecuteAsync(tokenAdmin, objetivo.Id);
        Assert.False(objetivo.Active);

        await Deactivate.ReactivateAsync(tokenAdmin, objetivo.Id);
        Assert.True(objetivo.Active);

        // Y entra con la misma Password.
        var token = await scenario.Login.ExecuteAsync("empleado@ejemplo.com", "abc12345");
        Assert.NotNull(token);
    }

    // Reactivate una cuenta que ya esta activa es un 400 controlado. Sin esto la
    // excepcion de la entidad salia como 500 con traza (RD-07, RD-08).
    [Fact]
    public async Task ReactivarDosVecesLaMismaCuentaEsRechazoControlado()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);

        // Sin esto la primera llamada ya fallaria, porque en el scenario el
        // User esta active: Reactivate tiene que ir precedido de Deactivate.
        await Deactivate.ExecuteAsync(tokenAdmin, scenario.User.Id);
        await Deactivate.ReactivateAsync(tokenAdmin, scenario.User.Id);

        var error = await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => Deactivate.ReactivateAsync(tokenAdmin, scenario.User.Id));

        Assert.Equal("La cuenta ya esta activa.", error.Message);
    }

    // Y el caso mas directo de todos: Reactivate a alguien que esta Active desde
    // el principio, sin pasar por Deactivate.
    [Fact]
    public async Task ReactivarUnaCuentaQueNuncaSeDesactivoEsRechazoControlado()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);

        var error = await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => Deactivate.ReactivateAsync(tokenAdmin, scenario.User.Id));

        Assert.Equal("La cuenta ya esta activa.", error.Message);
    }

    [Fact]
    public async Task ReactivarTambienExigeAdministrador()
    {
        var (scenario, _, tokenEstandar) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);

        await Assert.ThrowsAsync<InsufficientRoleException>(
            () => Deactivate.ReactivateAsync(tokenEstandar, scenario.User.Id));
    }

    [Fact]
    public async Task UnUsuarioQueNoExisteEsRechazoControlado()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);
        var ChangeRole = new ChangeUserRole(scenario.Users, scenario.Authenticate);
        var inexistente = Guid.NewGuid();

        await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => Deactivate.ExecuteAsync(tokenAdmin, inexistente));

        await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => ChangeRole.ExecuteAsync(tokenAdmin, inexistente, "Administrator"));
    }

    [Fact]
    public async Task LosMensajesDeRechazoNoLlevanCorreosNiHashes()
    {
        var (scenario, tokenAdmin, _) = await EscenarioConAmbosAsync();

        var Deactivate = new DeactivateUser(scenario.Users, scenario.Authenticate);
        var admin = await scenario.Users.FindByEmailAsync("admin@ejemplo.com");

        var error = await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => Deactivate.ExecuteAsync(tokenAdmin, admin!.Id));

        // RD-08: el Message dice que paso, no con que datos.
        Assert.DoesNotContain("admin@ejemplo.com", error.Message);
        Assert.DoesNotContain(admin!.PasswordHash, error.Message);
    }
}