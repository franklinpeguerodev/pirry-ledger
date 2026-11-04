using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Permissions;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-09 y docs/adr/014-password-change-permission.md, decision 4: el atajo del
// Administrator cuando el usuario olvido su contrasena.
//
// Force-reset invalida la contrasena anterior, concede ResetPassword y emite el
// codigo. Lo que estas pruebas fijan: que la contrasena vieja deja de servir, que
// el codigo emitido es el que completa la recuperacion, que repetir el atajo no
// acumula concesiones ni codigos, y que un Standard no puede usarlo.
public sealed class PruebasDeReinicioForzadoDeContrasena
{
    private static readonly DateTime utcNow = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string AdminEmail = "admin@ejemplo.com";
    private const string AdminPassword = "admin12345";
    private const string NewPassword = "nueva12345";

    private static async Task<Escenario> CrearEscenarioAsync()
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

        var codes = new InMemoryRecoveryCodeRepository();
        var queue = new InMemoryEmailQueue();
        var issuer = new PasswordResetIssuer(
            scenario.Users, codes, queue, scenario.Clock, RecoveryCodePolicy.Validity);
        var permissions = new FakePermissionAuthority(
            PermissionNames.ResetPassword,
            PermissionNames.ChangePassword);
        var requests = new FakePermissionRequestGateway();

        var caso = new ForcePasswordReset(
            scenario.Users,
            scenario.Authenticate,
            permissions,
            issuer,
            scenario.Clock,
            scenario.Hasher);

        var recuperacion = new PasswordRecovery(
            scenario.Users, codes, issuer, permissions, requests, scenario.Clock, scenario.Hasher);

        return new Escenario(
            scenario, tokenAdmin, codes, queue, permissions, requests, caso, recuperacion);
    }

    private static string CodigoDelCorreo(QueuedEmail email) =>
        email.Body.Split('\n', StringSplitOptions.TrimEntries)
            .Single(line => line.Length == OneTimeCodeGenerator.LengthInBytes * 2);

    [Fact]
    public async Task UnAdministradorInvalidaLaContrasenaYEmiteElCodigo()
    {
        var escenario = await CrearEscenarioAsync();

        await escenario.Caso.ExecuteAsync(escenario.TokenAdmin, escenario.Scenario.User.Id);

        // La contrasena anterior ya no sirve: el hash se reemplazo por uno
        // aleatorio que nadie conoce.
        Assert.False(escenario.Scenario.Hasher.Verify(
            escenario.Scenario.Password, escenario.Scenario.User.PasswordHash));

        // La concesion existe y el codigo se encolo.
        Assert.True(await escenario.Permissions.UserHasPermissionAsync(
            escenario.Scenario.User.Id, PermissionNames.ResetPassword));

        var correo = Assert.Single(escenario.Queue.Emails);
        Assert.Equal(escenario.Scenario.Email, correo.Recipient);
        Assert.Contains("4 horas", correo.Body);

        var codigo = Assert.Single(escenario.Codes.Saved);
        Assert.Equal(utcNow.Add(TimeSpan.FromHours(4)), codigo.ExpiresAtUtc);
    }

    [Fact]
    public async Task ElCodigoEmitidoEsElQueCompletaLaRecuperacion()
    {
        var escenario = await CrearEscenarioAsync();

        await escenario.Caso.ExecuteAsync(escenario.TokenAdmin, escenario.Scenario.User.Id);

        var codigo = CodigoDelCorreo(Assert.Single(escenario.Queue.Emails));

        await escenario.Recuperacion.CompleteAsync(codigo, NewPassword);

        Assert.True(escenario.Scenario.Hasher.Verify(
            NewPassword, escenario.Scenario.User.PasswordHash));

        // Y la concesion se consumio en el camino.
        Assert.False(await escenario.Permissions.UserHasPermissionAsync(
            escenario.Scenario.User.Id, PermissionNames.ResetPassword));
    }

    [Fact]
    public async Task RepetirElAtajoNoAcumulaConcesionesNiCodigos()
    {
        var escenario = await CrearEscenarioAsync();

        await escenario.Caso.ExecuteAsync(escenario.TokenAdmin, escenario.Scenario.User.Id);
        await escenario.Caso.ExecuteAsync(escenario.TokenAdmin, escenario.Scenario.User.Id);

        // Una sola concesion (upsert) y un solo codigo vigente: el segundo
        // invalido el primero antes de emitir.
        Assert.True(await escenario.Permissions.UserHasPermissionAsync(
            escenario.Scenario.User.Id, PermissionNames.ResetPassword));
        Assert.Single(escenario.Codes.Saved, code => !code.IsUsed);
        Assert.Equal(2, escenario.Queue.Emails.Count);
    }

    [Fact]
    public async Task UnEstandarNoPuedeReiniciarLaContrasenaDeNadie()
    {
        var escenario = await CrearEscenarioAsync();
        var tokenEstandar = await escenario.Scenario.OpenSessionAsync();
        var hashAntes = escenario.Scenario.User.PasswordHash;

        await Assert.ThrowsAsync<InsufficientRoleException>(
            () => escenario.Caso.ExecuteAsync(tokenEstandar, escenario.Scenario.User.Id));

        // Y nada cambio: ni hash, ni concesion, ni correo.
        Assert.Equal(hashAntes, escenario.Scenario.User.PasswordHash);
        Assert.False(await escenario.Permissions.UserHasPermissionAsync(
            escenario.Scenario.User.Id, PermissionNames.ResetPassword));
        Assert.Empty(escenario.Queue.Emails);
    }

    [Fact]
    public async Task ReiniciarUnUsuarioInexistenteEsRechazoControlado()
    {
        var escenario = await CrearEscenarioAsync();

        var error = await Assert.ThrowsAsync<AdministrationOperationRejectedException>(
            () => escenario.Caso.ExecuteAsync(escenario.TokenAdmin, Guid.NewGuid()));

        Assert.Equal("No existe ese usuario.", error.Message);
    }

    [Fact]
    public async Task SinSesionEs401YNo403()
    {
        var escenario = await CrearEscenarioAsync();

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => escenario.Caso.ExecuteAsync("token-inventado", escenario.Scenario.User.Id));
    }

    private sealed record Escenario(
        SessionScenario Scenario,
        string TokenAdmin,
        InMemoryRecoveryCodeRepository Codes,
        InMemoryEmailQueue Queue,
        FakePermissionAuthority Permissions,
        FakePermissionRequestGateway Requests,
        ForcePasswordReset Caso,
        PasswordRecovery Recuperacion);
}
