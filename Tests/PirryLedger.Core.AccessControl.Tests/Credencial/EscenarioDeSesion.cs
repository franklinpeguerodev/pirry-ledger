using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// scenario compartido por las pruebas de Session.
//
// Usa el Argon2id REAL, no uno de mentira. El tiempo que tarda (~130 ms por
// Operation) es el precio de comprobar de verdad que el hash senuelo se usa, que
// es el mecanismo anti-fuga de RF-CA-03.
internal sealed class SessionScenario
{
    private const string ValidPassword = "abc12345";
    private const string UserEmail = "empleado@ejemplo.com";

    public SessionScenario(DateTime? now = null)
    {
        var start = now ?? new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        Clock = new FakeClock(start);
        Users = new FakeUserRepository();
        Sessions = new FakeSessionRepository();

        // Parametros de Argon2 identicos a los de la aplicacion.
        Hasher = new Argon2idPasswordHasher(new Argon2Parameters(
            MemoryKb: 19456,
            Iterations: 2,
            Parallelism: 1));

        User = Users.Create("Empleado", UserEmail, Hasher.Hash(ValidPassword), start, active: true);

        Authenticate = new Authenticate(Sessions, Users, Clock);
        Login = new Login(Users, Sessions, Hasher, Clock, TimeSpan.FromHours(8));
        Logout = new Logout(Sessions, Clock);
    }

    public FakeClock Clock { get; }

    public FakeUserRepository Users { get; }

    public FakeSessionRepository Sessions { get; }

    public Argon2idPasswordHasher Hasher { get; }

    public User User { get; }

    public Authenticate Authenticate { get; }

    public Login Login { get; }

    public Logout Logout { get; }

    public string Email => UserEmail;

    public string Password => ValidPassword;

    // Inicia Session y devuelve el token en claro.
    public async Task<string> OpenSessionAsync()
    {
        return await Login.ExecuteAsync(UserEmail, ValidPassword);
    }

    // Devuelve el token con el que quedo abierta la Session, sin abrir una nueva.
    // Lo necesitan las pruebas de RF-CA-20: hay que comprobar que la credencial
    // que YA estaba emitida deja de servir, no que no se pueda emitir otra.
    public string LastToken { get; private set; } = string.Empty;

    // El token que se uso para esta prueba, guardado al abrir Session.
    public async Task<string> OpenSessionAndStoreTokenAsync()
    {
        LastToken = await Login.ExecuteAsync(UserEmail, ValidPassword);

        return LastToken;
    }
}
