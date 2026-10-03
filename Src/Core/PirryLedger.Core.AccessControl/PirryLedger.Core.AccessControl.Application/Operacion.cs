namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-05: "existe un punto del codigo donde se puede leer la exigencia de rol
// de cada operacion".
//
// Este enum nombra TODAS las operaciones del sistema, no solo las de
// Administracion. No dice quien puede hacer cada una: eso vive en
// ExigenciasDeRol, que es el punto unico. Aqui solo esta el NOMBRE, para que el
// punto unico pueda hablar de operaciones sin repetir cadenas de texto por ahi.
//
// Por que tambien las operaciones publicas:
//
// El enunciado pide "la exigencia de rol de cada operacion", no "de cada
// operacion que tenga un rol". Una ruta publica tiene una exigencia igual de
// clara: la de no exigir nada. Declararla aqui es lo que permite que la prueba
// que recorre las rutas del Host pueda afirmar que ninguna se queda sin
// declarar. Si estas quedaran fuera, esa prueba solo podria comprobar las que
// elijiera un清单 escrito a mano, que es justo lo que hay que evitar.
//
// Ojo con el nombre: una entrada en este enum NO abre ni cierra nada por si
// sola. Si se declara aqui y no en ExigenciasDeRol, la ruta se rechaza (ver
// EseAccesoFallo).
public enum Operacion
{
    // Registro y activacion. Abiertas a cualquiera: no hay sesion todavia.
    RegistrarUsuario,
    ActivarCuenta,
    ReenviarActivacion,

    // Inicio y cierre de sesion.
    IniciarSesion,

    // CerrarSesion NO es publica a proposito. Se responde 204 siempre, incluso
    // con una credencial caducada o inventada, para que cerrar la sesion no
    // sirva de oraculo sobre si un token existe. Es la unica excepcion a "exigir
    // una sesion valida", y se aplica en Autenticar, no aqui.
    CerrarSesion,

    // Consultar quien es. Cualquier sesion valida.
    ConsultarSesionPropia,

    // Administracion de usuarios: RF-CA-08, 20 y 21.
    ListarUsuarios,
    CambiarRolDeUsuario,
    DesactivarUsuario,
    ReactivarUsuario,

    SolicitarRecuperacion,
    RestablecerContrasena,
    CambiarContrasenaPropia,
    ForzarRestablecimiento,
}

// Los tres niveles de exigencia. El orden va de menos a mas: una ruta publica
// no necesita sesion, una de sesion acepta a cualquiera con una sesion valida, y
// una de Administrador exige ese rol concreto (RD-06).
public enum NivelAcceso
{
    Publico,
    CualquierSesion,
    Administrador,
}