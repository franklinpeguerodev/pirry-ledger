using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-06: 401 y 403 son cosas distintas y el punto de validacion las
// distingue.
//
// No es un detalle de Code de Status: un 401 dice "no se quien eres, identificate"
// y un 403 dice "se quien eres y no te alcanza". Confundirlos hace que el
// frontend pida la Password a un user que ya la puso bien, o que le oculte
// una Operation que si podria hacer.
public sealed class PruebasDeRolYEstado
{
    private static readonly TimeSpan VentanaDeSesion = TimeSpan.FromHours(8);

    private static async Task<SessionScenario> EscenarioConRolAsync(Role role)
    {
        var utcNow = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var scenario = new SessionScenario(utcNow);

        // El scenario ya trae un Standard. Se anade un segundo user con el Role
        // que interesa, para no tener que construir el scenario entero otra vez.
        scenario.Users.Create(
            "Con Role", "conrol@ejemplo.com", scenario.Hasher.Hash("abc12345"), utcNow, active: true, role: role);

        return scenario;
    }

    [Fact]
    public async Task RolSuficientePasaYDevuelveElUsuario()
    {
        var scenario = await EscenarioConRolAsync(Role.Administrator);
        var token = await scenario.Login.ExecuteAsync("conrol@ejemplo.com", "abc12345");

        var user = await scenario.Authenticate.ExecuteWithRoleAsync(token, Role.Administrator);

        Assert.Equal(Role.Administrator, user.Role);
        Assert.Equal("conrol@ejemplo.com", user.Email);
    }

    [Fact]
    public async Task RolInsuficienteLanzaLaExcepcionDeRolYNoLaDeSesion()
    {
        var scenario = await EscenarioConRolAsync(Role.Standard);
        var token = await scenario.Login.ExecuteAsync("conrol@ejemplo.com", "abc12345");

        // Este es el 403. Y NO es InvalidSessionException, que es el 401: por eso
        // estan separadas, para que el endpoint elija el Code solo mirando el
        // Type de la excepcion.
        var error = await Assert.ThrowsAsync<InsufficientRoleException>(
            () => scenario.Authenticate.ExecuteWithRoleAsync(token, Role.Administrator));

        Assert.NotNull(error);
    }

    [Fact]
    public async Task SinSesionEsUn401YConRolInsuficienteEsUn403()
    {
        var scenario = await EscenarioConRolAsync(Role.Standard);

        var sinToken = await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteWithRoleAsync(string.Empty, Role.Administrator));

        var token = await scenario.Login.ExecuteAsync("conrol@ejemplo.com", "abc12345");
        var rolInsuficiente = await Assert.ThrowsAsync<InsufficientRoleException>(
            () => scenario.Authenticate.ExecuteWithRoleAsync(token, Role.Administrator));

        // Dos tipos distintos a proposito: el endpoint traduce cada uno a su
        // Code. Si fueran el mismo Type, habria que adivinar cual de los dos
        // es y el 403 se perderia.
        Assert.IsType<InvalidSessionException>(sinToken);
        Assert.IsType<InsufficientRoleException>(rolInsuficiente);
        Assert.NotEqual(sinToken.GetType(), rolInsuficiente.GetType());
    }

    [Fact]
    public async Task ElRolSeCompruebaDESPUESDeValidarLaSesion()
    {
        var scenario = await EscenarioConRolAsync(Role.Standard);
        var token = await scenario.Login.ExecuteAsync("conrol@ejemplo.com", "abc12345");

        await scenario.Logout.ExecuteAsync(token);

        // session cerrada y Role insuficiente a la vez. Gana el 401, porque sin
        // session valida no hay nada que saber de roles: decir "no tienes
        // permisos" confirmaria que el token fue real alguna vez.
        var error = await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteWithRoleAsync(token, Role.Administrator));

        Assert.Equal(typeof(InvalidSessionException), error.GetType());
    }

    // RF-CA-08 + ADR 009: cambiar el role invalida las sesiones abiertas (mismo
    // mecanismo que ChangePassword y Deactivate). El usuario promovido (o
    // degradado) tiene que re-login para que su siguiente peticion refleje el
    // nuevo role.
    [Fact]
    public async Task CambiarDeRolInvalidaLaSesion()
    {
        var utcNow = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var scenario = new SessionScenario(utcNow);
        var user = scenario.Users.Create(
            "Cambio Role", "cambiorol@ejemplo.com", scenario.Hasher.Hash("abc12345"), utcNow, active: true, role: Role.Administrator);

        var token = await scenario.Login.ExecuteAsync("cambiorol@ejemplo.com", "abc12345");

        // Con Role de Administrator, entra.
        await scenario.Authenticate.ExecuteWithRoleAsync(token, Role.Administrator);

        user.ChangeRole(Role.Standard);

        // Tras el cambio, CredentialVersion se incremento y la session queda
        // invalidada: el token anterior ya no sirve ni siquiera para preguntar
        // quien es. La regla es la misma que para ChangePassword y Deactivate.
        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteAsync(token));
    }

    [Fact]
    public async Task HaySesionValidaEsTrueYFalseSinSesionNiTrasCerrar()
    {
        var scenario = new SessionScenario();
        var token = await scenario.OpenSessionAsync();

        Assert.True(await scenario.Authenticate.HasValidSessionAsync(token));

        await scenario.Logout.ExecuteAsync(token);

        Assert.False(await scenario.Authenticate.HasValidSessionAsync(token));
    }

    [Fact]
    public async Task DesactivarElUsuarioTambienCortaElAccesoConRol()
    {
        var utcNow = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var scenario = new SessionScenario(utcNow);
        var user = scenario.Users.Create(
            "Se Desactiva", "sedesactiva@ejemplo.com", scenario.Hasher.Hash("abc12345"), utcNow, active: true, role: Role.Administrator);

        var token = await scenario.Login.ExecuteAsync("sedesactiva@ejemplo.com", "abc12345");
        await scenario.Authenticate.ExecuteWithRoleAsync(token, Role.Administrator);

        user.Deactivate();

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteWithRoleAsync(token, Role.Administrator));
    }
}