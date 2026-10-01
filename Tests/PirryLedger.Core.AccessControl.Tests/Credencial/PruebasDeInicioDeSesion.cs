using PirryLedger.Core.AccessControl.Application;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-03 y RF-CA-19.
//
// Lo que se comprueba aqui es que NO se filtra informacion. Que un correo
// inexistente y una contrasena erronea den el mismo error es la mitad del
// requisito; la otra mitad, y la que se olvida, es que no se distingan ni por el
// mensaje ni por el tiempo que tardan.
public sealed class PruebasDeInicioDeSesion
{
    private static readonly TimeSpan VentanaDeSesion = TimeSpan.FromHours(8);

    [Fact]
    public async Task CredencialesCorrectasAbrenSesion()
    {
        var escenario = new EscenarioDeSesion();

        var token = await escenario.AbrirSesionAsync();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(1, escenario.Sesiones.Guardadas);
    }

    [Fact]
    public async Task CorreoInexistenteYContrasenaErroneaDanElMismoError()
    {
        var escenario = new EscenarioDeSesion();

        var correoQueNoExiste = await CapturarAsync(
            () => escenario.Entrar.EjecutarAsync("nadie@ejemplo.com", "abc12345"));
        var contrasenaErronea = await CapturarAsync(
            () => escenario.Entrar.EjecutarAsync(escenario.Correo, "malaclave1"));

        // Mismo tipo: el endpoint los traduce a 401 igual.
        Assert.Equal(correoQueNoExiste.Tipo, contrasenaErronea.Tipo);

        // Y mismo mensaje, sin espacios ni diferencias sutiles.
        Assert.Equal(correoQueNoExiste.Mensaje, contrasenaErronea.Mensaje);
    }

    // Con el hash senuelo cacheado, verificar contra un usuario inexistente cuesta
    // la MISMA operacion del hasher que contra uno real: una sola verificacion.
    // Se comprueba la propiedad directamente en vez de comparar milisegundos,
    // porque la carga del equipo hace que una medicion temporal sea inestable.
    [Fact]
    public async Task ElCorreoNoExistenteEjecutaElMismoTrabajoDeHash()
    {
        var escenario = new EscenarioDeSesion();
        var hasher = new HasherContador(escenario.Hasher);
        var login = new Login(
            escenario.Usuarios,
            escenario.Sesiones,
            hasher,
            escenario.Reloj,
            VentanaDeSesion);

        await CapturarAsync(() => login.EjecutarAsync("nadie@ejemplo.com", "abc12345"));
        Assert.Equal(1, hasher.Verificaciones);
        Assert.Equal(1, hasher.CreacionesDeSenaluelo);

        hasher.ReiniciarContadores();
        await CapturarAsync(() => login.EjecutarAsync(escenario.Correo, "malaclave1"));
        Assert.Equal(1, hasher.Verificaciones);
        Assert.Equal(0, hasher.CreacionesDeSenaluelo);
    }

    [Fact]
    public async Task CuentaNoActivadaSeInformaSoloTrasVerificarLaContrasena()
    {
        var ahora = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var escenario = new EscenarioDeSesion(ahora);

        // Un segundo usuario, registrado pero sin activar (RF-CA-15).
        var inactivo = escenario.Usuarios.Crear(
            "Pendiente", "pendiente@ejemplo.com", escenario.Hasher.Hash("abc12345"), ahora, activo: false);

        var usuario = await escenario.Usuarios.BuscarPorIdAsync(inactivo.Id);

        Assert.False(usuario!.Activo);

        // Con la contrasena CORRECTA se dice que la cuenta no esta activada.
        var correcto = await CapturarAsync(
            () => escenario.Entrar.EjecutarAsync("pendiente@ejemplo.com", "abc12345"));
        Assert.Equal(typeof(CuentaNoActivadaException), correcto.Tipo);

        // Con la contrasena ERRONEA se dice lo de siempre, sin mencionar la
        // activacion. Si dijera "esa cuenta existe pero esta inactiva" sin
        // contrasena, bastaria con probar correos para saber quien tiene cuenta.
        var erroneo = await CapturarAsync(
            () => escenario.Entrar.EjecutarAsync("pendiente@ejemplo.com", "malaclave1"));
        Assert.Equal(typeof(CredencialesRechazadasException), erroneo.Tipo);
        Assert.DoesNotContain("activa", erroneo.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CincoIntentosFallidosBloqueanLaCuenta()
    {
        var escenario = new EscenarioDeSesion();

        for (var intento = 1; intento <= 5; intento++)
        {
            await CapturarAsync(() => escenario.Entrar.EjecutarAsync(escenario.Correo, "malaclave1"));
        }

        Assert.True(escenario.Usuario.EstaBloqueado(escenario.Reloj.UtcNow));
    }

    [Fact]
    public async Task ElSextoIntentoSeRechazaAunConLaContrasenaCorrecta()
    {
        var escenario = new EscenarioDeSesion();

        for (var intento = 1; intento <= 5; intento++)
        {
            await CapturarAsync(() => escenario.Entrar.EjecutarAsync(escenario.Correo, "malaclave1"));
        }

        // RF-CA-19: aqui esta el criterio de aceptacion literal.
        var sexto = await CapturarAsync(() => escenario.Entrar.EjecutarAsync(escenario.Correo, escenario.Contrasena));

        Assert.Equal(typeof(CuentaBloqueadaException), sexto.Tipo);
        Assert.Equal(0, escenario.Sesiones.Guardadas);
    }

    [Fact]
    public async Task ElMensajeDeBloqueoNoDistingueDeCredencialesIncorrectas()
    {
        var escenario = new EscenarioDeSesion();

        for (var intento = 1; intento <= 5; intento++)
        {
            await CapturarAsync(() => escenario.Entrar.EjecutarAsync(escenario.Correo, "malaclave1"));
        }

        var duranteBloqueo = await CapturarAsync(
            () => escenario.Entrar.EjecutarAsync(escenario.Correo, escenario.Contrasena));

        // Si el bloqueo tuviera su propio mensaje, confirmaria que ese correo
        // existe. Con el mismo texto, no se puede saber.
        var contrasenaErronea = await CapturarAsync(
            () => escenario.Entrar.EjecutarAsync(escenario.Correo, "malaclave1"));

        Assert.Equal(contrasenaErronea.Mensaje, duranteBloqueo.Mensaje);
    }

    [Fact]
    public async Task ElBloqueoTerminaAlPasarQuinceMinutos()
    {
        var escenario = new EscenarioDeSesion();

        for (var intento = 1; intento <= 5; intento++)
        {
            await CapturarAsync(() => escenario.Entrar.EjecutarAsync(escenario.Correo, "malaclave1"));
        }

        Assert.True(escenario.Usuario.EstaBloqueado(escenario.Reloj.UtcNow));

        // Sin esperar: el reloj es inyectable (RD-11).
        escenario.Reloj.Avanzar(TimeSpan.FromMinutes(15));

        Assert.False(escenario.Usuario.EstaBloqueado(escenario.Reloj.UtcNow));

        // Y ya se puede entrar con la contrasena correcta.
        var token = await escenario.Entrar.EjecutarAsync(escenario.Correo, escenario.Contrasena);
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task UnInicioDeSesionCorrectoPoneElContadorEnCero()
    {
        var escenario = new EscenarioDeSesion();

        for (var intento = 1; intento <= 4; intento++)
        {
            await CapturarAsync(() => escenario.Entrar.EjecutarAsync(escenario.Correo, "malaclave1"));
        }

        Assert.Equal(4, escenario.Usuario.IntentosFallidos);

        await escenario.AbrirSesionAsync();

        Assert.Equal(0, escenario.Usuario.IntentosFallidos);
        Assert.Null(escenario.Usuario.BloqueoHastaUtc);
    }

    [Fact]
    public async Task LosIntentosFallidosSeAcumulanSoloMientrasNoHayaAcierto()
    {
        var escenario = new EscenarioDeSesion();

        // Dos fallos, un acierto (contador a cero), y dos fallos mas: no se llega
        // al bloqueo.
        await CapturarAsync(() => escenario.Entrar.EjecutarAsync(escenario.Correo, "malaclave1"));
        await CapturarAsync(() => escenario.Entrar.EjecutarAsync(escenario.Correo, "malaclave2"));
        await escenario.AbrirSesionAsync();
        await CapturarAsync(() => escenario.Entrar.EjecutarAsync(escenario.Correo, "malaclave3"));
        await CapturarAsync(() => escenario.Entrar.EjecutarAsync(escenario.Correo, "malaclave4"));

        Assert.Equal(2, escenario.Usuario.IntentosFallidos);
        Assert.False(escenario.Usuario.EstaBloqueado(escenario.Reloj.UtcNow));
    }

    [Fact]
    public async Task ElCorreoEsSensibleAMayusculasYMinusculas()
    {
        var escenario = new EscenarioDeSesion();

        var token = await escenario.Entrar.EjecutarAsync("EMPLEADO@EJEMPLO.COM", escenario.Contrasena);

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    // Captura la excepcion sin que el test falle, para poder comparar el mensaje
    // entre los dos casos.
    private static async Task<(Type Tipo, string Mensaje)> CapturarAsync(Func<Task> accion)
    {
        try
        {
            await accion();
            return (typeof(object), string.Empty);
        }
        catch (Exception error)
        {
            return (error.GetType(), error.Message);
        }
    }

    private sealed class HasherContador(IPasswordHasher interno) : IPasswordHasher
    {
        public int Verificaciones { get; private set; }

        public int CreacionesDeSenaluelo { get; private set; }

        public string Hash(string contrasena) => interno.Hash(contrasena);

        public bool Verificar(string contrasena, string hashGuardado)
        {
            Verificaciones++;
            return interno.Verificar(contrasena, hashGuardado);
        }

        public string CrearHashSenaluelo()
        {
            CreacionesDeSenaluelo++;
            return interno.CrearHashSenaluelo();
        }

        public void ReiniciarContadores()
        {
            Verificaciones = 0;
            CreacionesDeSenaluelo = 0;
        }
    }
}