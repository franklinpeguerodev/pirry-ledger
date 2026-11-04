using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Permissions;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-12 y docs/adr/014-password-change-permission.md.
//
// Un Standard con sesion no cambia su contrasena sin aprobacion. Lo que estas
// pruebas fijan:
//
// - Sin concesion ChangePassword, el cambio se rechaza (403), la contrasena no
//   cambia y se crea una solicitud.
// - Un segundo intento con la solicitud ya pendiente devuelve otro rechazo
//   (409) y no crea otra solicitud.
// - Con la concesion, el cambio ocurre y la concesion se consume una sola vez.
// - La contrasena actual equivocada no gasta la concesion.
// - Un Administrator esta exento: con un solo Administrator, exigir aprobacion
//   haria imposible que cambiara su propia contrasena.
public sealed class PruebasDeAprobacionDeCambioDeContrasena
{
    private static readonly DateTime utcNow = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string NewPassword = "nueva12345";

    private static async Task<Escenario> CrearEscenarioAsync()
    {
        var scenario = new SessionScenario(utcNow);
        var permissions = new FakePermissionAuthority(
            PermissionNames.ChangePassword,
            PermissionNames.ResetPassword);
        var requests = new FakePermissionRequestGateway();
        var caso = new ChangeOwnPassword(
            scenario.Users, scenario.Hasher, scenario.Authenticate, permissions, requests, scenario.Clock);
        var token = await scenario.OpenSessionAsync();

        return new Escenario(scenario, permissions, requests, caso, token);
    }

    [Fact]
    public async Task UnEstandarSinAprobacionNoCambiaSuContrasenaYPideLaSolicitud()
    {
        var escenario = await CrearEscenarioAsync();

        await Assert.ThrowsAsync<PasswordChangeApprovalRequiredException>(
            () => escenario.Caso.ExecuteAsync(
                escenario.Token, escenario.Scenario.Password, NewPassword));

        // La contrasena no cambio y la solicitud quedo pendiente.
        Assert.True(escenario.Scenario.Hasher.Verify(
            escenario.Scenario.Password, escenario.Scenario.User.PasswordHash));

        var pendiente = Assert.Single(escenario.Requests.Pending);
        Assert.Equal(escenario.Scenario.User.Id, pendiente.RequesterId);
        Assert.Equal(PermissionNames.ChangePassword, pendiente.Permission);
    }

    [Fact]
    public async Task UnSegundoIntentoConLaSolicitudPendienteNoCreaOtra()
    {
        var escenario = await CrearEscenarioAsync();

        await Assert.ThrowsAsync<PasswordChangeApprovalRequiredException>(
            () => escenario.Caso.ExecuteAsync(
                escenario.Token, escenario.Scenario.Password, NewPassword));

        // ADR 014: la solicitud ya existe, asi que el segundo intento es 409.
        await Assert.ThrowsAsync<PasswordChangeApprovalPendingException>(
            () => escenario.Caso.ExecuteAsync(
                escenario.Token, escenario.Scenario.Password, NewPassword));

        Assert.Single(escenario.Requests.Pending);
        Assert.Equal(1, escenario.Requests.CreatedCount);
        Assert.True(escenario.Scenario.Hasher.Verify(
            escenario.Scenario.Password, escenario.Scenario.User.PasswordHash));
    }

    [Fact]
    public async Task ConLaAprobacionElEstandarCambiaLaContrasenaYConsumeLaConcesion()
    {
        var escenario = await CrearEscenarioAsync();
        escenario.Permissions.Grant(
            escenario.Scenario.User.Id, PermissionNames.ChangePassword);

        await escenario.Caso.ExecuteAsync(
            escenario.Token, escenario.Scenario.Password, NewPassword);

        Assert.True(escenario.Scenario.Hasher.Verify(
            NewPassword, escenario.Scenario.User.PasswordHash));

        // La concesion se consumio: no vale para un segundo cambio.
        Assert.False(await escenario.Permissions.UserHasPermissionAsync(
            escenario.Scenario.User.Id, PermissionNames.ChangePassword));
    }

    [Fact]
    public async Task LaContrasenaActualEquivocadaNoGastaLaConcesion()
    {
        var escenario = await CrearEscenarioAsync();
        escenario.Permissions.Grant(
            escenario.Scenario.User.Id, PermissionNames.ChangePassword);

        await Assert.ThrowsAsync<RegistrationRejectedException>(
            () => escenario.Caso.ExecuteAsync(
                escenario.Token, "equivocada123", NewPassword));

        // Nada cambio: ni la contrasena ni la concesion.
        Assert.True(escenario.Scenario.Hasher.Verify(
            escenario.Scenario.Password, escenario.Scenario.User.PasswordHash));
        Assert.True(await escenario.Permissions.UserHasPermissionAsync(
            escenario.Scenario.User.Id, PermissionNames.ChangePassword));
    }

    [Fact]
    public async Task UnAdministradorCambiaSuContrasenaSinAprobacion()
    {
        var scenario = new SessionScenario(utcNow);
        var admin = scenario.Users.Create(
            "Admin",
            "admin@ejemplo.com",
            scenario.Hasher.Hash("admin12345"),
            utcNow,
            active: true,
            role: Role.Administrator);

        var token = await scenario.Login.ExecuteAsync("admin@ejemplo.com", "admin12345");

        var permissions = new FakePermissionAuthority(PermissionNames.ChangePassword);
        var requests = new FakePermissionRequestGateway();
        var caso = new ChangeOwnPassword(
            scenario.Users, scenario.Hasher, scenario.Authenticate, permissions, requests, scenario.Clock);

        await caso.ExecuteAsync(token, "admin12345", NewPassword);

        Assert.True(scenario.Hasher.Verify(NewPassword, admin.PasswordHash));
        Assert.Empty(requests.Pending);
    }

    [Fact]
    public async Task SinSesionElCambioEs401YNoPideNada()
    {
        var escenario = await CrearEscenarioAsync();

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => escenario.Caso.ExecuteAsync(
                "token-inventado", escenario.Scenario.Password, NewPassword));

        Assert.Empty(escenario.Requests.Pending);
    }

    private sealed record Escenario(
        SessionScenario Scenario,
        FakePermissionAuthority Permissions,
        FakePermissionRequestGateway Requests,
        ChangeOwnPassword Caso,
        string Token);
}
