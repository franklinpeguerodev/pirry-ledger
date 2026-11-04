using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Permissions;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-09, RF-CA-10 y RF-CA-11, y docs/adr/014-password-change-permission.md.
//
// Lo que estas pruebas fijan, y que cambio con ADR 014:
//
// - Un Standard NO recibe codigo al pedir la recuperacion: se crea una
//   solicitud ResetPassword y el codigo sale cuando un Administrator la aprueba.
// - Un Administrator esta exento y recibe su codigo de inmediato, porque con un
//   solo Administrator RF-GP-07 haria imposible aprobar su propia solicitud.
// - El codigo vale cuatro horas, no quince minutos.
// - Completar la recuperacion consume la concesion una sola vez.
public sealed class PruebasDeRecuperacionDeContrasena
{
    private static readonly DateTime utcNow = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string StandardEmail = "empleado@ejemplo.com";
    private const string AdminEmail = "admin@ejemplo.com";
    private const string OldPassword = "old12345";
    private const string NewPassword = "new12345";

    private static Escenario CrearEscenario(Role role = Role.Standard)
    {
        var clock = new FakeClock(utcNow);
        var users = new FakeUserRepository();
        var email = role == Role.Administrator ? AdminEmail : StandardEmail;
        var user = users.Create("Ana", email, $"hash:{OldPassword}", utcNow, active: true, role: role);
        var codes = new InMemoryRecoveryCodeRepository();
        var queue = new InMemoryEmailQueue();
        var hasher = new FakePasswordHasher();
        var issuer = new PasswordResetIssuer(
            users, codes, queue, clock, RecoveryCodePolicy.Validity);
        var permissions = new FakePermissionAuthority(
            PermissionNames.ResetPassword,
            PermissionNames.ChangePassword);
        var requests = new FakePermissionRequestGateway();
        var caso = new PasswordRecovery(
            users, codes, issuer, permissions, requests, clock, hasher);

        return new Escenario(clock, users, user, codes, queue, permissions, requests, issuer, caso);
    }

    // Extrae el codigo en claro del cuerpo del correo. Solo vive ahi.
    private static string CodigoDelCorreo(QueuedEmail email) =>
        email.Body.Split('\n', StringSplitOptions.TrimEntries)
            .Single(line => line.Length == OneTimeCodeGenerator.LengthInBytes * 2);

    [Fact]
    public async Task UnEstandarNoRecibeCodigoHastaQueUnAdministradorAprueba()
    {
        var escenario = CrearEscenario();

        await escenario.Caso.RequestAsync(StandardEmail);

        // ADR 014, decision 2: ni correo ni codigo todavia.
        Assert.Empty(escenario.Queue.Emails);
        Assert.Empty(escenario.Codes.Saved);

        // Lo que si ocurrio: la solicitud quedo pendiente.
        var pendiente = Assert.Single(escenario.Requests.Pending);
        Assert.Equal(escenario.User.Id, pendiente.RequesterId);
        Assert.Equal(PermissionNames.ResetPassword, pendiente.Permission);
    }

    [Fact]
    public async Task UnAdministradorRecibeSuCodigoSinAprobacion()
    {
        var escenario = CrearEscenario(Role.Administrator);

        await escenario.Caso.RequestAsync(AdminEmail);

        // ADR 014, decision 6: el Administrator esta exento.
        var correo = Assert.Single(escenario.Queue.Emails);
        Assert.Equal(AdminEmail, correo.Recipient);
        Assert.Single(escenario.Codes.Saved);
        Assert.Empty(escenario.Requests.Pending);

        // El texto del correo sigue la duracion real, no un numero escrito a mano.
        Assert.Contains("4 horas", correo.Body);

        // Y con el codigo se completa sin concesion: no hay aprobacion de nadie.
        await escenario.Caso.CompleteAsync(CodigoDelCorreo(correo), NewPassword);

        Assert.Equal($"hash:{NewPassword}", escenario.User.PasswordHash);
    }

    [Fact]
    public async Task ElCodigoEsDeUnSoloUsoYExpiraEnCuatroHoras()
    {
        var escenario = CrearEscenario(Role.Administrator);

        // Primer codigo: se usa una vez y no sirve una segunda.
        await escenario.Caso.RequestAsync(AdminEmail);
        var primero = CodigoDelCorreo(escenario.Queue.Emails[0]);

        await escenario.Caso.CompleteAsync(primero, NewPassword);
        await Assert.ThrowsAsync<RecoveryRejectedException>(
            () => escenario.Caso.CompleteAsync(primero, "other123"));

        // Segundo codigo: se deja vencer.
        await escenario.Caso.RequestAsync(AdminEmail);
        var segundo = CodigoDelCorreo(escenario.Queue.Emails[1]);
        var codigoGuardado = escenario.Codes.Saved.Single(code => !code.IsUsed);

        Assert.Equal(utcNow.Add(TimeSpan.FromHours(4)), codigoGuardado.ExpiresAtUtc);

        escenario.Clock.Advance(TimeSpan.FromHours(4));

        await Assert.ThrowsAsync<RecoveryRejectedException>(
            () => escenario.Caso.CompleteAsync(segundo, "other123"));
    }

    [Fact]
    public async Task ElEstandarNecesitaLaConcesionYLaConsumeUnaSolaVez()
    {
        var escenario = CrearEscenario();

        await escenario.Caso.RequestAsync(StandardEmail);

        // Simula la aprobacion: el Administrator concedio ResetPassword y el
        // codigo se emitio entonces (ApprovePermissionRequest lo hace).
        escenario.Permissions.Grant(escenario.User.Id, PermissionNames.ResetPassword);
        await escenario.Issuer.IssueAsync(escenario.User.Id);

        var codigo = CodigoDelCorreo(Assert.Single(escenario.Queue.Emails));

        // Con la concesion, el codigo completa la recuperacion...
        await escenario.Caso.CompleteAsync(codigo, NewPassword);
        Assert.Equal($"hash:{NewPassword}", escenario.User.PasswordHash);

        // ...y la concesion queda consumida: el mismo codigo ya no sirve.
        Assert.False(await escenario.Permissions.UserHasPermissionAsync(
            escenario.User.Id, PermissionNames.ResetPassword));
        await Assert.ThrowsAsync<RecoveryRejectedException>(
            () => escenario.Caso.CompleteAsync(codigo, "other123"));
    }

    [Fact]
    public async Task UnaSolicitudDeCorreoDesconocidoNoHaceNada()
    {
        var escenario = CrearEscenario();

        await escenario.Caso.RequestAsync("no-existe@ejemplo.com");

        Assert.Empty(escenario.Queue.Emails);
        Assert.Empty(escenario.Codes.Saved);
        Assert.Empty(escenario.Requests.Pending);
    }

    // ADR 014, decision 7: la duracion es una sola constante y el texto del
    // correo se deriva de ella.
    [Fact]
    public void LaVigenciaDelCodigoEsDeCuatroHoras()
    {
        Assert.Equal(TimeSpan.FromHours(4), RecoveryCodePolicy.Validity);
        Assert.Equal("4 horas", RecoveryCodePolicy.Describe(RecoveryCodePolicy.Validity));
    }

    private sealed record Escenario(
        FakeClock Clock,
        FakeUserRepository Users,
        User User,
        InMemoryRecoveryCodeRepository Codes,
        InMemoryEmailQueue Queue,
        FakePermissionAuthority Permissions,
        FakePermissionRequestGateway Requests,
        PasswordResetIssuer Issuer,
        PasswordRecovery Caso);
}
