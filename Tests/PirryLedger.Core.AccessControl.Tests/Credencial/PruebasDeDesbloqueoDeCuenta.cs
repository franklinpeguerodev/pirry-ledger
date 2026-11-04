using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-19 y docs/adr/013-admin-created-users.md, decision 6: el Administrator
// desbloquea una cuenta sin esperar los quince minutos del bloqueo automatico.
//
// Lo que fijan estas pruebas: que el desbloqueo devuelve la cuenta, que es
// idempotente, que no cambia la contrasena ni invalida sesiones, y que un
// Standard no puede saltarse el bloqueo por su cuenta.
public sealed class PruebasDeDesbloqueoDeCuenta
{
    private static readonly DateTime utcNow = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string AdminEmail = "admin@ejemplo.com";
    private const string AdminPassword = "admin12345";
    private const string WrongPassword = "equivocada123";

    private static async Task<(SessionScenario scenario, string TokenAdmin, UnlockUser Caso)> EscenarioAsync()
    {
        var scenario = new SessionScenario(utcNow);

        scenario.Users.Create(
            "Admin",
            AdminEmail,
            scenario.Hasher.Hash(AdminPassword),
            utcNow,
            active: true,
            role: Role.Administrator);

        var tokenAdmin = await scenario.Login.ExecuteAsync(AdminEmail, AdminPassword);

        return (scenario, tokenAdmin, new UnlockUser(scenario.Users, scenario.Authenticate));
    }

    // RF-CA-19: cinco intentos fallidos dejan la cuenta bloqueada.
    private static async Task BloquearAsync(SessionScenario scenario)
    {
        for (var intento = 0; intento < 5; intento++)
        {
            await Assert.ThrowsAsync<CredentialsRejectedException>(
                () => scenario.Login.ExecuteAsync(scenario.Email, WrongPassword));
        }

        Assert.True(scenario.User.IsBlocked(utcNow));
    }

    [Fact]
    public async Task AdministradorDesbloqueaUnaCuentaBloqueada()
    {
        var (scenario, tokenAdmin, caso) = await EscenarioAsync();

        await BloquearAsync(scenario);

        // Con el bloqueo vigente, ni la contrasena correcta entra.
        await Assert.ThrowsAsync<AccountBlockedException>(
            () => scenario.Login.ExecuteAsync(scenario.Email, scenario.Password));

        await caso.ExecuteAsync(tokenAdmin, scenario.User.Id);

        Assert.False(scenario.User.IsBlocked(utcNow));
        Assert.Equal(0, scenario.User.FailedAttempts);

        // Y la cuenta vuelve a entrar con la misma contrasena.
        var token = await scenario.Login.ExecuteAsync(scenario.Email, scenario.Password);

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    // ADR 013, decision 6: desbloquear es idempotente. El Administrator pide un
    // estado, no una transicion, asi que el segundo intento tampoco falla.
    [Fact]
    public async Task DesbloquearDosVecesNoEsUnError()
    {
        var (scenario, tokenAdmin, caso) = await EscenarioAsync();

        await BloquearAsync(scenario);

        await caso.ExecuteAsync(tokenAdmin, scenario.User.Id);
        await caso.ExecuteAsync(tokenAdmin, scenario.User.Id);

        Assert.Equal(0, scenario.User.FailedAttempts);
        Assert.Null(scenario.User.LockedUntilUtc);
    }

    // Desbloquear no cambia la contrasena ni invalida sesiones: no toca el hash
    // ni CredentialVersion. La sesion que ya estaba abierta sigue sirviendo.
    [Fact]
    public async Task DesbloquearNoCambiaLaContrasenaNiInvalidaLaSesion()
    {
        var (scenario, tokenAdmin, caso) = await EscenarioAsync();

        var tokenObjetivo = await scenario.OpenSessionAsync();
        var hashAntes = scenario.User.PasswordHash;
        var versionAntes = scenario.User.CredentialVersion;

        await BloquearAsync(scenario);
        await caso.ExecuteAsync(tokenAdmin, scenario.User.Id);

        Assert.Equal(hashAntes, scenario.User.PasswordHash);
        Assert.Equal(versionAntes, scenario.User.CredentialVersion);

        var autenticado = await scenario.Authenticate.ExecuteAsync(tokenObjetivo);

        Assert.Equal(scenario.User.Id, autenticado.Id);
    }

    // RD-06: un Standard no se desbloquea a si mismo, ni con una peticion armada
    // a mano. Su sesion sigue siendo valida durante el bloqueo (el bloqueo solo
    // impide iniciar sesion), asi que este es el intento realista.
    [Fact]
    public async Task UnEstandarNoPuedeDesbloquearSuPropiaCuentaBloqueada()
    {
        var (scenario, _, caso) = await EscenarioAsync();

        var tokenEstandar = await scenario.OpenSessionAsync();

        await BloquearAsync(scenario);

        await Assert.ThrowsAsync<InsufficientRoleException>(
            () => caso.ExecuteAsync(tokenEstandar, scenario.User.Id));

        // Y sigue bloqueado: el rechazo no desbloqueo nada.
        Assert.True(scenario.User.IsBlocked(utcNow));
    }

    [Fact]
    public async Task DesbloquearUnUsuarioQueNoExisteEsRechazoControlado()
    {
        var (scenario, tokenAdmin, caso) = await EscenarioAsync();

        var error = await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => caso.ExecuteAsync(tokenAdmin, Guid.NewGuid()));

        Assert.Equal("No existe ese usuario.", error.Message);
    }

    [Fact]
    public async Task SinSesionEs401YNo403()
    {
        var (scenario, _, caso) = await EscenarioAsync();

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => caso.ExecuteAsync(string.Empty, scenario.User.Id));

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => caso.ExecuteAsync("token-inventado", scenario.User.Id));
    }
}
