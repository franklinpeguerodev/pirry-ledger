using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-18 y RF-CA-12: una credencial deja de servir en cuanto pasa algo.
//
// El criterio de aceptacion de RF-CA-18 es "la credencial cerrada deja de servir:
// usarla despues se rechaza". Estas pruebas comprueban las cuatro maneras de
// invalidarla que enumera current-iteration.md: Close session, cambiar la
// Password, restablecerla y Deactivate al user.
public sealed class PruebasDeInvalidacionDeCredencial
{
    private static readonly TimeSpan VentanaDeSesion = TimeSpan.FromHours(8);

    [Fact]
    public async Task UnaSesionValidaPermiteConsultarQuienEres()
    {
        var scenario = new SessionScenario();
        var token = await scenario.OpenSessionAsync();

        var user = await scenario.Authenticate.ExecuteAsync(token);

        Assert.Equal(scenario.User.Id, user.Id);
        Assert.Equal("empleado@ejemplo.com", user.Email);
    }

    [Fact]
    public async Task CerrarSesionInvalidaEsaCredencial()
    {
        var scenario = new SessionScenario();
        var token = await scenario.OpenSessionAsync();

        // Antes de Close, funciona.
        await scenario.Authenticate.ExecuteAsync(token);

        await scenario.Logout.ExecuteAsync(token);

        // RF-CA-18: usarla despues se rechaza.
        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteAsync(token));
    }

    [Fact]
    public async Task CerrarSesionNoInvalidaLasSesionesOtrasDelMismoUsuario()
    {
        var scenario = new SessionScenario();

        var tokenMovil = await scenario.OpenSessionAsync();
        var tokenPortatil = await scenario.OpenSessionAsync();

        Assert.Equal(2, scenario.Sessions.SavedCount);

        await scenario.Logout.ExecuteAsync(tokenMovil);

        // El portatil sigue abierto: Close el movil no echa del otro dispositivo.
        var user = await scenario.Authenticate.ExecuteAsync(tokenPortatil);
        Assert.Equal(scenario.User.Id, user.Id);

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteAsync(tokenMovil));
    }

    [Fact]
    public async Task CerrarDosVecesLaMismaSesionNoFalla()
    {
        var scenario = new SessionScenario();
        var token = await scenario.OpenSessionAsync();

        await scenario.Logout.ExecuteAsync(token);

        // Dos pestanas cerrando a la vez no es un failure del user (RF-CA-18).
        await scenario.Logout.ExecuteAsync(token);
    }

    [Fact]
    public async Task UnTokenQueNoExisteSeRechazaSinLanzarErrorDistinto()
    {
        var scenario = new SessionScenario();

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteAsync("token-inventado"));

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteAsync(string.Empty));
    }

    [Fact]
    public async Task CambiarLaContrasenaInvalidaLasCredencialesYaEmitidas()
    {
        var scenario = new SessionScenario();
        var tokenAntiguo = await scenario.OpenSessionAsync();
        var cambioUtc = scenario.Clock.UtcNow;

        // RF-CA-12: el cambio sube CredentialVersion.
        scenario.User.ChangePassword(scenario.Hasher.Hash("nueva12345"), cambioUtc);

        Assert.Equal(cambioUtc, scenario.User.PasswordChangedAtUtc);

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteAsync(tokenAntiguo));
    }

    [Fact]
    public async Task RestablecerLaContrasenaInvalidaLasCredencialesYaEmitidas()
    {
        var scenario = new SessionScenario();
        var tokenAntiguo = await scenario.OpenSessionAsync();

        // RF-CA-13: el restablecimiento forzado pasa por el mismo camino que un
        // cambio normal, asi que sube la version igual.
        scenario.User.ChangePassword(scenario.Hasher.Hash("restablecida1"), scenario.Clock.UtcNow);

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteAsync(tokenAntiguo));

        // Y con la Password nueva si se puede Login de nuevo.
        var tokenNuevo = await scenario.Login.ExecuteAsync(scenario.Email, "restablecida1");
        await scenario.Authenticate.ExecuteAsync(tokenNuevo);
    }

    [Fact]
    public async Task DesactivarAlUsuarioInvalidaSuSesionAbierta()
    {
        var scenario = new SessionScenario();
        var token = await scenario.OpenSessionAsync();

        // RF-CA-20: Deactivate con una session abierta la tira.
        scenario.User.Deactivate();

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteAsync(token));
    }

    [Fact]
    public async Task UnaSesionVencidaSeRechaza()
    {
        var scenario = new SessionScenario();
        var token = await scenario.OpenSessionAsync();

        // Todavia no ha vencido.
        await scenario.Authenticate.ExecuteAsync(token);

        // Se pasa el vencimiento absoluto de 8 horas, sin esperar: el Clock es
        // inyectable (RD-11).
        scenario.Clock.Advance(VentanaDeSesion + TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteAsync(token));
    }

    [Fact]
    public async Task UnaSesionNoSeRenuevaAlUsarla()
    {
        var scenario = new SessionScenario();
        var token = await scenario.OpenSessionAsync();

        // Decision del ADR: vencimiento absoluto, no deslizante. Aunque se use
        // sin parar, no se estira.
        //
        // 23 vueltas de 20 minutos son 460 minutos: 7 h 40 min, todavia dentro de
        // las 8 horas.
        for (var i = 0; i < 23; i++)
        {
            scenario.Clock.Advance(TimeSpan.FromMinutes(20));
            await scenario.Authenticate.ExecuteAsync(token);
        }

        // Se pasa de las 8 horas (490 minutos) sin haber cerrado session. Con
        // vencimiento deslizante esto seguiria vivo; con absoluto, no.
        scenario.Clock.Advance(TimeSpan.FromMinutes(30));

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => scenario.Authenticate.ExecuteAsync(token));
    }

    [Fact]
    public async Task ElTokenNoSeGuardaEnClaroEnLaBase()
    {
        var scenario = new SessionScenario();
        var token = await scenario.OpenSessionAsync();

        var session = scenario.Sessions.Find(SessionTokenGenerator.CalculateHash(token));

        Assert.NotNull(session);
        Assert.NotEqual(token, session.TokenHash);
        Assert.DoesNotContain(token, session.TokenHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElTokenEsDistintoEnCadaInicioDeSesion()
    {
        var scenario = new SessionScenario();

        var primero = await scenario.OpenSessionAsync();
        var segundo = await scenario.OpenSessionAsync();

        Assert.NotEqual(primero, segundo);
    }

    [Fact]
    public async Task ElTokenNoUsaCaracteresQueNoViajenEnUnaCabecera()
    {
        var scenario = new SessionScenario();
        var token = await scenario.OpenSessionAsync();

        // base64url: sin '+', sin '/' y sin '='. Con la base64 Standard el token
        // no podria ir en Authorization sin escapar.
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }
}