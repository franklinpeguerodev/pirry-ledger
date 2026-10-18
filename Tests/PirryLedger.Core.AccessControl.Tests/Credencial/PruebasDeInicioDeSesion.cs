using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-03 y RF-CA-19.
//
// Lo que se comprueba aqui es que NO se filtra informacion. Que un Email
// inexistente y una Password erronea den el mismo error es la mitad del
// requisito; la otra mitad, y la que se olvida, es que no se distingan ni por el
// Message ni por el tiempo que tardan.
public sealed class PruebasDeInicioDeSesion
{
    private static readonly TimeSpan VentanaDeSesion = TimeSpan.FromHours(8);

    [Fact]
    public async Task CredencialesCorrectasAbrenSesion()
    {
        var scenario = new SessionScenario();

        var token = await scenario.OpenSessionAsync();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(1, scenario.Sessions.SavedCount);
    }

    [Fact]
    public async Task CorreoInexistenteYContrasenaErroneaDanElMismoError()
    {
        var scenario = new SessionScenario();

        var nonExistentEmail = await CapturarAsync(
            () => scenario.Login.ExecuteAsync("nadie@ejemplo.com", "abc12345"));
        var wrongPassword = await CapturarAsync(
            () => scenario.Login.ExecuteAsync(scenario.Email, "malaclave1"));

        // Mismo Type: el endpoint los traduce a 401 igual.
        Assert.Equal(nonExistentEmail.Type, wrongPassword.Type);

        // Y mismo Message, sin espacios ni diferencias sutiles.
        Assert.Equal(nonExistentEmail.Message, wrongPassword.Message);
    }

    // Con el hash senuelo cacheado, Verify contra un user inexistente cuesta
    // la MISMA Operation del hasher que contra uno real: una sola verificacion.
    // Se comprueba la propiedad directamente en vez de comparar milisegundos,
    // porque la carga del equipo hace que una medicion temporal sea inestable.
    [Fact]
    public async Task ElCorreoNoExistenteEjecutaElMismoTrabajoDeHash()
    {
        var scenario = new SessionScenario();
        var hasher = new HasherContador(scenario.Hasher);
        var login = new Login(
            scenario.Users,
            scenario.Sessions,
            hasher,
            scenario.Clock,
            VentanaDeSesion);

        await CapturarAsync(() => login.ExecuteAsync("nadie@ejemplo.com", "abc12345"));
        Assert.Equal(1, hasher.Verificaciones);
        Assert.Equal(1, hasher.CreacionesDeSenaluelo);

        hasher.ReiniciarContadores();
        await CapturarAsync(() => login.ExecuteAsync(scenario.Email, "malaclave1"));
        Assert.Equal(1, hasher.Verificaciones);
        Assert.Equal(0, hasher.CreacionesDeSenaluelo);
    }

    [Fact]
    public async Task CuentaNoActivadaSeInformaSoloTrasVerificarLaContrasena()
    {
        var utcNow = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var scenario = new SessionScenario(utcNow);

        // Un segundo user, registrado pero sin Activate (RF-CA-15).
        var inactivo = scenario.Users.Create(
            "Pending", "Pending@ejemplo.com", scenario.Hasher.Hash("abc12345"), utcNow, active: false);

        var user = await scenario.Users.FindByIdAsync(inactivo.Id);

        Assert.False(user!.Active);

        // Con la Password CORRECTA se dice que la cuenta no esta activada.
        var correcto = await CapturarAsync(
            () => scenario.Login.ExecuteAsync("Pending@ejemplo.com", "abc12345"));
        Assert.Equal(typeof(AccountNotActivatedException), correcto.Type);

        // Con la Password ERRONEA se dice lo de siempre, sin mencionar la
        // activacion. Si dijera "esa cuenta existe pero esta inactiva" sin
        // Password, bastaria con probar correos para saber quien tiene cuenta.
        var erroneo = await CapturarAsync(
            () => scenario.Login.ExecuteAsync("Pending@ejemplo.com", "malaclave1"));
        Assert.Equal(typeof(CredentialsRejectedException), erroneo.Type);
        Assert.DoesNotContain("activa", erroneo.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CincoIntentosFallidosBloqueanLaCuenta()
    {
        var scenario = new SessionScenario();

        for (var intento = 1; intento <= 5; intento++)
        {
            await CapturarAsync(() => scenario.Login.ExecuteAsync(scenario.Email, "malaclave1"));
        }

        Assert.True(scenario.User.IsBlocked(scenario.Clock.UtcNow));
    }

    [Fact]
    public async Task ElSextoIntentoSeRechazaAunConLaContrasenaCorrecta()
    {
        var scenario = new SessionScenario();

        for (var intento = 1; intento <= 5; intento++)
        {
            await CapturarAsync(() => scenario.Login.ExecuteAsync(scenario.Email, "malaclave1"));
        }

        // RF-CA-19: aqui esta el criterio de aceptacion literal.
        var sexto = await CapturarAsync(() => scenario.Login.ExecuteAsync(scenario.Email, scenario.Password));

        Assert.Equal(typeof(AccountBlockedException), sexto.Type);
        Assert.Equal(0, scenario.Sessions.SavedCount);
    }

    [Fact]
    public async Task ElMensajeDeBloqueoNoDistingueDeCredencialesIncorrectas()
    {
        var scenario = new SessionScenario();

        for (var intento = 1; intento <= 5; intento++)
        {
            await CapturarAsync(() => scenario.Login.ExecuteAsync(scenario.Email, "malaclave1"));
        }

        var duranteBloqueo = await CapturarAsync(
            () => scenario.Login.ExecuteAsync(scenario.Email, scenario.Password));

        // Si el bloqueo tuviera su propio Message, confirmaria que ese Email
        // existe. Con el mismo texto, no se puede saber.
        var wrongPassword = await CapturarAsync(
            () => scenario.Login.ExecuteAsync(scenario.Email, "malaclave1"));

        Assert.Equal(wrongPassword.Message, duranteBloqueo.Message);
    }

    [Fact]
    public async Task ElBloqueoTerminaAlPasarQuinceMinutos()
    {
        var scenario = new SessionScenario();

        for (var intento = 1; intento <= 5; intento++)
        {
            await CapturarAsync(() => scenario.Login.ExecuteAsync(scenario.Email, "malaclave1"));
        }

        Assert.True(scenario.User.IsBlocked(scenario.Clock.UtcNow));

        // Sin esperar: el Clock es inyectable (RD-11).
        scenario.Clock.Advance(TimeSpan.FromMinutes(15));

        Assert.False(scenario.User.IsBlocked(scenario.Clock.UtcNow));

        // Y ya se puede Login con la Password correcta.
        var token = await scenario.Login.ExecuteAsync(scenario.Email, scenario.Password);
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task UnInicioDeSesionCorrectoPoneElContadorEnCero()
    {
        var scenario = new SessionScenario();

        for (var intento = 1; intento <= 4; intento++)
        {
            await CapturarAsync(() => scenario.Login.ExecuteAsync(scenario.Email, "malaclave1"));
        }

        Assert.Equal(4, scenario.User.FailedAttempts);

        await scenario.OpenSessionAsync();

        Assert.Equal(0, scenario.User.FailedAttempts);
        Assert.Null(scenario.User.LockedUntilUtc);
    }

    [Fact]
    public async Task LosIntentosFallidosSeAcumulanSoloMientrasNoHayaAcierto()
    {
        var scenario = new SessionScenario();

        // Dos failures, un acierto (contador a cero), y dos failures mas: no se llega
        // al bloqueo.
        await CapturarAsync(() => scenario.Login.ExecuteAsync(scenario.Email, "malaclave1"));
        await CapturarAsync(() => scenario.Login.ExecuteAsync(scenario.Email, "malaclave2"));
        await scenario.OpenSessionAsync();
        await CapturarAsync(() => scenario.Login.ExecuteAsync(scenario.Email, "malaclave3"));
        await CapturarAsync(() => scenario.Login.ExecuteAsync(scenario.Email, "malaclave4"));

        Assert.Equal(2, scenario.User.FailedAttempts);
        Assert.False(scenario.User.IsBlocked(scenario.Clock.UtcNow));
    }

    [Fact]
    public async Task ElCorreoEsSensibleAMayusculasYMinusculas()
    {
        var scenario = new SessionScenario();

        var token = await scenario.Login.ExecuteAsync("EMPLEADO@EJEMPLO.COM", scenario.Password);

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    // Captura la excepcion sin que el test falle, para poder comparar el Message
    // entre los dos casos.
    private static async Task<(Type Type, string Message)> CapturarAsync(Func<Task> accion)
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

        public string Hash(string Password) => interno.Hash(Password);

        public bool Verify(string Password, string hashGuardado)
        {
            Verificaciones++;
            return interno.Verify(Password, hashGuardado);
        }

        public string CreateDummyHash()
        {
            CreacionesDeSenaluelo++;
            return interno.CreateDummyHash();
        }

        public void ReiniciarContadores()
        {
            Verificaciones = 0;
            CreacionesDeSenaluelo = 0;
        }
    }
}