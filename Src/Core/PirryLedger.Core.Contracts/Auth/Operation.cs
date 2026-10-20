namespace PirryLedger.Core.Contracts.Auth;

// RF-CA-05: "existe un punto del codigo donde se puede leer la Requirement de role
// de cada operation".
//
// Este enum nombra TODAS las operaciones del sistema, no solo las de
// Administracion. No dice quien puede hacer cada una: eso vive en
// RoleRequirements, que es el punto unico. Aqui solo esta el Name, para que el
// punto unico pueda hablar de operaciones sin repetir cadenas de texto por ahi.
//
// Por que tambien las operaciones publicas:
//
// El enunciado pide "la Requirement de role de cada operation", no "de cada
// Operation que tenga un role". Una route publica tiene una Requirement igual de
// clara: la de no exigir nada. Declararla aqui es lo que permite que la prueba
// que recorre las rutas del Host pueda afirmar que ninguna se queda sin
// declarar. Si estas quedaran fuera, esa prueba solo podria comprobar las que
// elijiera un清单 escrito a mano, que es justo lo que hay que evitar.
//
// Ojo con el Name: una entrada en este enum NO abre ni cierra nada por si
// sola. Si se declara aqui y no en RoleRequirements, la route se rechaza.
public enum Operation
{
    // Registro y activacion. Abiertas a cualquiera: no hay Session todavia.
    RegisterUser,
    ActivateAccount,
    ResendActivation,

    // Inicio y cierre de Session.
    Login,

    // Logout NO es publica a proposito. Se responde 204 siempre, incluso
    // con una credencial caducada o inventada, para que Close la Session no
    // sirva de oraculo sobre si un token existe. Es la unica excepcion a "exigir
    // una Session valida", y se aplica en Authenticate, no aqui.
    Logout,

    // Consultar quien es. Cualquier Session valida.
    QueryOwnSession,

    // Administracion de usuarios: RF-CA-08, 20 y 21.
    ListUsers,
    ChangeUserRole,
    DeactivateUser,
    ReactivateUser,

    RequestRecovery,
    ResetPassword,
    ChangeOwnPassword,
    ForceReset,
}

// Los tres niveles de Requirement. El orden va de menos a mas: una route publica
// no necesita Session, una de Session acepta a cualquiera con una Session valida, y
// una de Administrator exige ese role concreto (RD-06).
public enum AccessLevel
{
    Public,
    AnySession,
    Administrator,
}