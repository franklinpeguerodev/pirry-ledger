using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Infrastructure;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// Escenario compartido por las pruebas de sesion.
//
// Usa el Argon2id REAL, no uno de mentira. El tiempo que tarda (~130 ms por
// operacion) es el precio de comprobar de verdad que el hash senuelo se usa, que
// es el mecanismo anti-fuga de RF-CA-03.
internal sealed class EscenarioDeSesion
{
    private const string ContrasenaValida = "abc12345";
    private const string CorreoDelUsuario = "empleado@ejemplo.com";

    public EscenarioDeSesion(DateTime? ahora = null)
    {
        var inicio = ahora ?? new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        Reloj = new RelojFalso(inicio);
        Usuarios = new UsuarioEnMemoria();
        Sesiones = new SesionEnMemoria();

        // Parametros de Argon2 identicos a los de la aplicacion.
        Hasher = new Argon2idPasswordHasher(new ParametrosDeArgon2(
            MemoriaKb: 19456,
            Iteraciones: 2,
            Paralelismo: 1));

        Usuario = Usuarios.Crear("Empleado", CorreoDelUsuario, Hasher.Hash(ContrasenaValida), inicio, activo: true);

        Autenticar = new Autenticar(Sesiones, Usuarios, Reloj);
        Entrar = new Login(Usuarios, Sesiones, Hasher, Reloj, TimeSpan.FromHours(8));
        Salir = new Logout(Sesiones, Reloj);
    }

    public RelojFalso Reloj { get; }

    public UsuarioEnMemoria Usuarios { get; }

    public SesionEnMemoria Sesiones { get; }

    public Argon2idPasswordHasher Hasher { get; }

    public Domain.Usuario Usuario { get; }

    public Autenticar Autenticar { get; }

    public Login Entrar { get; }

    public Logout Salir { get; }

    public string Correo => CorreoDelUsuario;

    public string Contrasena => ContrasenaValida;

    // Inicia sesion y devuelve el token en claro.
    public async Task<string> AbrirSesionAsync()
    {
        return await Entrar.EjecutarAsync(CorreoDelUsuario, ContrasenaValida);
    }

    // Devuelve el token con el que quedo abierta la sesion, sin abrir una nueva.
    // Lo necesitan las pruebas de RF-CA-20: hay que comprobar que la credencial
    // que YA estaba emitida deja de servir, no que no se pueda emitir otra.
    public string UltimoToken { get; private set; } = string.Empty;

    // El token que se uso para esta prueba, guardado al abrir sesion.
    public async Task<string> AbrirSesionYGuardarTokenAsync()
    {
        UltimoToken = await Entrar.EjecutarAsync(CorreoDelUsuario, ContrasenaValida);

        return UltimoToken;
    }
}