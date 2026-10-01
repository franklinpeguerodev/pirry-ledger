namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-05: el punto unico del sistema donde se lee que acceso exige cada
// operacion.
//
// La tabla esta aqui, entera, y en ningun otro sitio. Un endpoint no escribe
// "si es Administrador" ni compara roles: se declara con el nombre de su
// Operacion y el filtro de acceso consulta esta tabla.
//
// Por que una tabla y no un if en cada endpoint:
//
// - Si cada endpoint decidiera su acceso, la exigencia quedaria repartida en
//   varios ficheros y no se podria leer de un vistazo. El revisor tendria que
//   abrirlos todos para saber quien puede tocar la administracion de usuarios.
// - Con una tabla, cambiar "estas operaciones son de Administrador" se cambia en
//   una linea, y anadir una operacion obliga a declararla. Ojo: esto NO lo
//   comprueba el compilador. Una entrada olvidada compila sin problemas. Lo que
//   la delata son dos pruebas: una recorre el enum entero y la otra recorre las
//   rutas reales del Host y exige que cada una tenga entrada aqui.
// - El rechazo sigue ocurriendo en el SERVIDOR y no en la interfaz (RD-06).
//
// Que cubra tambien las operaciones publicas es deliberado. RF-CA-05 pide "la
// exigencia de rol de cada operacion del sistema", y una ruta publica declara
// que no exige nada. Si vivieran solo las de Administrador, esta seria una tabla
// de administracion, no un punto unico del sistema, y no se podria afirmar que
// ninguna ruta se quedo sin declarar.
public static class ExigenciasDeRol
{
    // La tabla. Toda la exigencia de acceso del sistema esta en estas lineas.
    //
    // El segundo campo, tolerante, es la unica excepcion a la regla y esta aqui
    // a proposito, no en el endpoint: cerrar sesion tiene que responder 204
    // siempre (decision de Franklin, RF-CA-18), incluso con una credencial
    // caducada o inventada. Si el filtro exigiera una sesion valida, ese endpoint
    // devolveria 401 con token caducado y 500 con token corrupto, y ademas
    // serviria de oraculo sobre si un token existo alguna vez.
    private static readonly Dictionary<Operacion, Exigencia> Exigencias = new()
    {
        // RF-CA-01 y RF-CA-02: registro, activacion y reenvio ocurren antes de
        // que exista una sesion, asi que son publicos. El reenvio responde igual
        // haya o no correo, y por eso no puede filtrar por usuario.
        [Operacion.RegistrarUsuario] = new(NivelAcceso.Publico),
        [Operacion.ActivarCuenta] = new(NivelAcceso.Publico),
        [Operacion.ReenviarActivacion] = new(NivelAcceso.Publico),

        // RF-CA-03: el inicio de sesion es publico por definicion, es la operacion
        // que devuelve la credencial. Si exigiera sesion no podria usarse nunca.
        [Operacion.IniciarSesion] = new(NivelAcceso.Publico),

        // RF-CA-04: cerrar sesion es de "cualquier sesion" pero NO llega a
        // comprobar que la sesion siga viva. Se responde 204 siempre, con token
        // valido, caducado o inventado, para no convertir el cierre en un
        // oraculo sobre si un token existe. De ahi tolerante: true.
        [Operacion.CerrarSesion] = new(NivelAcceso.CualquierSesion, Tolerante: true),

        // /yo: basta con una sesion valida. No es de Administrador porque un
        // Estandar tambien necesita saber quien es.
        [Operacion.ConsultarSesionPropia] = new(NivelAcceso.CualquierSesion),

        // RF-CA-21: listar usuarios es de Administrador, y un Estandar recibe
        // rechazo (el mismo 403 que en las otras tres).
        [Operacion.ListarUsuarios] = new(NivelAcceso.Administrador),

        // RF-CA-08: cambiar el rol lo hace un Administrador.
        [Operacion.CambiarRolDeUsuario] = new(NivelAcceso.Administrador),

        // RF-CA-20: desactivar y reactivar los hace un Administrador.
        [Operacion.DesactivarUsuario] = new(NivelAcceso.Administrador),
        [Operacion.ReactivarUsuario] = new(NivelAcceso.Administrador),

        [Operacion.SolicitarRecuperacion] = new(NivelAcceso.Publico),
        [Operacion.RestablecerContrasena] = new(NivelAcceso.Publico),
        [Operacion.CambiarContrasenaPropia] = new(NivelAcceso.CualquierSesion),
        [Operacion.ForzarRestablecimiento] = new(NivelAcceso.Administrador),
    };

    // La exigencia completa de una operacion: que nivel pide y si tolera que la
    // credencial no sea valida.
    public sealed record Exigencia(NivelAcceso Nivel, bool Tolerante = false);

    // Que acceso exige la operacion. Si alguien anade una operacion al enum y
    // olvida esta tabla, esto lanza al SER llamada, en vez de dejar la operacion
    // abierta sin proteccion.
    public static NivelAcceso NivelRequerido(Operacion operacion) =>
        ExigenciaDe(operacion).Nivel;

    // Si la operacion tolera una credencial no valida. Lo consulta el filtro para
    // no exigir una sesion que el endpoint no necesita.
    public static bool ToleraCredencialInvalida(Operacion operacion) =>
        ExigenciaDe(operacion).Tolerante;

    private static Exigencia ExigenciaDe(Operacion operacion)
    {
        if (!Exigencias.TryGetValue(operacion, out var exigencia))
        {
            throw new OperacionSinExigenciaDeRolException(operacion);
        }

        return exigencia;
    }

    // El rol concreto cuando la operacion exige uno. Las operaciones que no son
    // de Administrador no tienen rol que comprobar, y preguntar por el suyo
    // seria un error de programacion, no un caso de negocio: por eso lanza
    // ArgumentException en vez de inventar un valor.
    public static Domain.Rol RolRequerido(Operacion operacion) =>
        NivelRequerido(operacion) switch
        {
            NivelAcceso.Administrador => Domain.Rol.Administrador,
            _ => throw new ArgumentException(
                $"La operacion {operacion} no exige un rol concreto: es {NivelRequerido(operacion)}.",
                nameof(operacion)),
        };

    // Todas las operaciones declaradas. Lo usan las pruebas que verifican que la
    // tabla no tenga huecos, en los dos sentidos: cada operacion del enum tiene
    // que estar aqui, y cada entrada tiene que existir en el enum.
    public static IReadOnlyCollection<Operacion> OperacionesDeclaradas() =>
        Exigencias.Keys.ToArray();
}