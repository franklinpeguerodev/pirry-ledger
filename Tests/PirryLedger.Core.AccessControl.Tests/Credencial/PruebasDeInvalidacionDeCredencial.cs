using PirryLedger.Core.AccessControl.Application;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-18 y RF-CA-12: una credencial deja de servir en cuanto pasa algo.
//
// El criterio de aceptacion de RF-CA-18 es "la credencial cerrada deja de servir:
// usarla despues se rechaza". Estas pruebas comprueban las cuatro maneras de
// invalidarla que enumera current-iteration.md: cerrar sesion, cambiar la
// contrasena, restablecerla y desactivar al usuario.
public sealed class PruebasDeInvalidacionDeCredencial
{
    private static readonly TimeSpan VentanaDeSesion = TimeSpan.FromHours(8);

    [Fact]
    public async Task UnaSesionValidaPermiteConsultarQuienEres()
    {
        var escenario = new EscenarioDeSesion();
        var token = await escenario.AbrirSesionAsync();

        var usuario = await escenario.Autenticar.EjecutarAsync(token);

        Assert.Equal(escenario.Usuario.Id, usuario.Id);
        Assert.Equal("empleado@ejemplo.com", usuario.Correo);
    }

    [Fact]
    public async Task CerrarSesionInvalidaEsaCredencial()
    {
        var escenario = new EscenarioDeSesion();
        var token = await escenario.AbrirSesionAsync();

        // Antes de cerrar, funciona.
        await escenario.Autenticar.EjecutarAsync(token);

        await escenario.Salir.EjecutarAsync(token);

        // RF-CA-18: usarla despues se rechaza.
        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarAsync(token));
    }

    [Fact]
    public async Task CerrarSesionNoInvalidaLasSesionesOtrasDelMismoUsuario()
    {
        var escenario = new EscenarioDeSesion();

        var tokenMovil = await escenario.AbrirSesionAsync();
        var tokenPortatil = await escenario.AbrirSesionAsync();

        Assert.Equal(2, escenario.Sesiones.Guardadas);

        await escenario.Salir.EjecutarAsync(tokenMovil);

        // El portatil sigue abierto: cerrar el movil no echa del otro dispositivo.
        var usuario = await escenario.Autenticar.EjecutarAsync(tokenPortatil);
        Assert.Equal(escenario.Usuario.Id, usuario.Id);

        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarAsync(tokenMovil));
    }

    [Fact]
    public async Task CerrarDosVecesLaMismaSesionNoFalla()
    {
        var escenario = new EscenarioDeSesion();
        var token = await escenario.AbrirSesionAsync();

        await escenario.Salir.EjecutarAsync(token);

        // Dos pestanas cerrando a la vez no es un fallo del usuario (RF-CA-18).
        await escenario.Salir.EjecutarAsync(token);
    }

    [Fact]
    public async Task UnTokenQueNoExisteSeRechazaSinLanzarErrorDistinto()
    {
        var escenario = new EscenarioDeSesion();

        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarAsync("token-inventado"));

        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarAsync(string.Empty));
    }

    [Fact]
    public async Task CambiarLaContrasenaInvalidaLasCredencialesYaEmitidas()
    {
        var escenario = new EscenarioDeSesion();
        var tokenAntiguo = await escenario.AbrirSesionAsync();

        // RF-CA-12: el cambio sube CredencialVersion.
        escenario.Usuario.CambiarContrasena(escenario.Hasher.Hash("nueva12345"), escenario.Reloj.UtcNow);

        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarAsync(tokenAntiguo));
    }

    [Fact]
    public async Task RestablecerLaContrasenaInvalidaLasCredencialesYaEmitidas()
    {
        var escenario = new EscenarioDeSesion();
        var tokenAntiguo = await escenario.AbrirSesionAsync();

        // RF-CA-13: el restablecimiento forzado pasa por el mismo camino que un
        // cambio normal, asi que sube la version igual.
        escenario.Usuario.CambiarContrasena(escenario.Hasher.Hash("restablecida1"), escenario.Reloj.UtcNow);

        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarAsync(tokenAntiguo));

        // Y con la contrasena nueva si se puede entrar de nuevo.
        var tokenNuevo = await escenario.Entrar.EjecutarAsync(escenario.Correo, "restablecida1");
        await escenario.Autenticar.EjecutarAsync(tokenNuevo);
    }

    [Fact]
    public async Task DesactivarAlUsuarioInvalidaSuSesionAbierta()
    {
        var escenario = new EscenarioDeSesion();
        var token = await escenario.AbrirSesionAsync();

        // RF-CA-20: desactivar con una sesion abierta la tira.
        escenario.Usuario.Desactivar();

        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarAsync(token));
    }

    [Fact]
    public async Task UnaSesionVencidaSeRechaza()
    {
        var escenario = new EscenarioDeSesion();
        var token = await escenario.AbrirSesionAsync();

        // Todavia no ha vencido.
        await escenario.Autenticar.EjecutarAsync(token);

        // Se pasa el vencimiento absoluto de 8 horas, sin esperar: el reloj es
        // inyectable (RD-11).
        escenario.Reloj.Avanzar(VentanaDeSesion + TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarAsync(token));
    }

    [Fact]
    public async Task UnaSesionNoSeRenuevaAlUsarla()
    {
        var escenario = new EscenarioDeSesion();
        var token = await escenario.AbrirSesionAsync();

        // Decision del ADR: vencimiento absoluto, no deslizante. Aunque se use
        // sin parar, no se estira.
        //
        // 23 vueltas de 20 minutos son 460 minutos: 7 h 40 min, todavia dentro de
        // las 8 horas.
        for (var i = 0; i < 23; i++)
        {
            escenario.Reloj.Avanzar(TimeSpan.FromMinutes(20));
            await escenario.Autenticar.EjecutarAsync(token);
        }

        // Se pasa de las 8 horas (490 minutos) sin haber cerrado sesion. Con
        // vencimiento deslizante esto seguiria vivo; con absoluto, no.
        escenario.Reloj.Avanzar(TimeSpan.FromMinutes(30));

        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarAsync(token));
    }

    [Fact]
    public async Task ElTokenNoSeGuardaEnClaroEnLaBase()
    {
        var escenario = new EscenarioDeSesion();
        var token = await escenario.AbrirSesionAsync();

        var sesion = escenario.Sesiones.Buscar(SessionTokenGenerator.CalcularHash(token));

        Assert.NotNull(sesion);
        Assert.NotEqual(token, sesion.HashDelToken);
        Assert.DoesNotContain(token, sesion.HashDelToken, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElTokenEsDistintoEnCadaInicioDeSesion()
    {
        var escenario = new EscenarioDeSesion();

        var primero = await escenario.AbrirSesionAsync();
        var segundo = await escenario.AbrirSesionAsync();

        Assert.NotEqual(primero, segundo);
    }

    [Fact]
    public async Task ElTokenNoUsaCaracteresQueNoViajenEnUnaCabecera()
    {
        var escenario = new EscenarioDeSesion();
        var token = await escenario.AbrirSesionAsync();

        // base64url: sin '+', sin '/' y sin '='. Con la base64 estandar el token
        // no podria ir en Authorization sin escapar.
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }
}