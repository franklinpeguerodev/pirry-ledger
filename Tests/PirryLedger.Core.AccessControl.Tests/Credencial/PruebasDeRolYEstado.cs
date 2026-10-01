using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-06: 401 y 403 son cosas distintas y el punto de validacion las
// distingue.
//
// No es un detalle de codigo de estado: un 401 dice "no se quien eres, identificate"
// y un 403 dice "se quien eres y no te alcanza". Confundirlos hace que el
// frontend pida la contrasena a un usuario que ya la puso bien, o que le oculte
// una operacion que si podria hacer.
public sealed class PruebasDeRolYEstado
{
    private static readonly TimeSpan VentanaDeSesion = TimeSpan.FromHours(8);

    private static async Task<EscenarioDeSesion> EscenarioConRolAsync(Rol rol)
    {
        var ahora = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var escenario = new EscenarioDeSesion(ahora);

        // El escenario ya trae un Estandar. Se anade un segundo usuario con el rol
        // que interesa, para no tener que construir el escenario entero otra vez.
        escenario.Usuarios.Crear(
            "Con Rol", "conrol@ejemplo.com", escenario.Hasher.Hash("abc12345"), ahora, activo: true, rol: rol);

        return escenario;
    }

    [Fact]
    public async Task RolSuficientePasaYDevuelveElUsuario()
    {
        var escenario = await EscenarioConRolAsync(Rol.Administrador);
        var token = await escenario.Entrar.EjecutarAsync("conrol@ejemplo.com", "abc12345");

        var usuario = await escenario.Autenticar.EjecutarConRolAsync(token, Rol.Administrador);

        Assert.Equal(Rol.Administrador, usuario.Rol);
        Assert.Equal("conrol@ejemplo.com", usuario.Correo);
    }

    [Fact]
    public async Task RolInsuficienteLanzaLaExcepcionDeRolYNoLaDeSesion()
    {
        var escenario = await EscenarioConRolAsync(Rol.Estandar);
        var token = await escenario.Entrar.EjecutarAsync("conrol@ejemplo.com", "abc12345");

        // Este es el 403. Y NO es SesionInvalidaException, que es el 401: por eso
        // estan separadas, para que el endpoint elija el codigo solo mirando el
        // tipo de la excepcion.
        var error = await Assert.ThrowsAsync<RolInsuficienteException>(
            () => escenario.Autenticar.EjecutarConRolAsync(token, Rol.Administrador));

        Assert.NotNull(error);
    }

    [Fact]
    public async Task SinSesionEsUn401YConRolInsuficienteEsUn403()
    {
        var escenario = await EscenarioConRolAsync(Rol.Estandar);

        var sinToken = await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarConRolAsync(string.Empty, Rol.Administrador));

        var token = await escenario.Entrar.EjecutarAsync("conrol@ejemplo.com", "abc12345");
        var rolInsuficiente = await Assert.ThrowsAsync<RolInsuficienteException>(
            () => escenario.Autenticar.EjecutarConRolAsync(token, Rol.Administrador));

        // Dos tipos distintos a proposito: el endpoint traduce cada uno a su
        // codigo. Si fueran el mismo tipo, habria que adivinar cual de los dos
        // es y el 403 se perderia.
        Assert.IsType<SesionInvalidaException>(sinToken);
        Assert.IsType<RolInsuficienteException>(rolInsuficiente);
        Assert.NotEqual(sinToken.GetType(), rolInsuficiente.GetType());
    }

    [Fact]
    public async Task ElRolSeCompruebaDESPUESDeValidarLaSesion()
    {
        var escenario = await EscenarioConRolAsync(Rol.Estandar);
        var token = await escenario.Entrar.EjecutarAsync("conrol@ejemplo.com", "abc12345");

        await escenario.Salir.EjecutarAsync(token);

        // Sesion cerrada y rol insuficiente a la vez. Gana el 401, porque sin
        // sesion valida no hay nada que saber de roles: decir "no tienes
        // permisos" confirmaria que el token fue real alguna vez.
        var error = await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarConRolAsync(token, Rol.Administrador));

        Assert.Equal(typeof(SesionInvalidaException), error.GetType());
    }

    [Fact]
    public async Task CambiarDeRolNoInvalidaLaSesion()
    {
        var ahora = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var escenario = new EscenarioDeSesion(ahora);
        var usuario = escenario.Usuarios.Crear(
            "Cambio Rol", "cambiorol@ejemplo.com", escenario.Hasher.Hash("abc12345"), ahora, activo: true, rol: Rol.Administrador);

        var token = await escenario.Entrar.EjecutarAsync("cambiorol@ejemplo.com", "abc12345");

        // Con rol de Administrador, entra.
        await escenario.Autenticar.EjecutarConRolAsync(token, Rol.Administrador);

        usuario.CambiarRol(Rol.Estandar);

        // Tras el cambio, la sesion sigue viva (no se toco CredencialVersion),
        // pero ya no alcanza para lo de Administrador.
        await escenario.Autenticar.EjecutarAsync(token);
        await Assert.ThrowsAsync<RolInsuficienteException>(
            () => escenario.Autenticar.EjecutarConRolAsync(token, Rol.Administrador));
    }

    [Fact]
    public async Task HaySesionValidaEsTrueYFalseSinSesionNiTrasCerrar()
    {
        var escenario = new EscenarioDeSesion();
        var token = await escenario.AbrirSesionAsync();

        Assert.True(await escenario.Autenticar.HaySesionValidaAsync(token));

        await escenario.Salir.EjecutarAsync(token);

        Assert.False(await escenario.Autenticar.HaySesionValidaAsync(token));
    }

    [Fact]
    public async Task DesactivarElUsuarioTambienCortaElAccesoConRol()
    {
        var ahora = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var escenario = new EscenarioDeSesion(ahora);
        var usuario = escenario.Usuarios.Crear(
            "Se Desactiva", "sedesactiva@ejemplo.com", escenario.Hasher.Hash("abc12345"), ahora, activo: true, rol: Rol.Administrador);

        var token = await escenario.Entrar.EjecutarAsync("sedesactiva@ejemplo.com", "abc12345");
        await escenario.Autenticar.EjecutarConRolAsync(token, Rol.Administrador);

        usuario.Desactivar();

        await Assert.ThrowsAsync<SesionInvalidaException>(
            () => escenario.Autenticar.EjecutarConRolAsync(token, Rol.Administrador));
    }
}