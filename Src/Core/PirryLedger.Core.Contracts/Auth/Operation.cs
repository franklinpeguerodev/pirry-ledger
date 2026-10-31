namespace PirryLedger.Core.Contracts.Auth;

// RF-CA-05: "Existe un punto del código donde se puede leer la exigencia de rol
// de cada operación".
//
// Este enum nombra TODAS las operaciones del sistema, no solo las de
// Administracion. No dice quien puede hacer cada una: eso vive en
// RoleRequirements, que es el punto unico. Aqui solo esta el Name, para que el
// punto unico pueda hablar de operaciones sin repetir cadenas de texto por ahi.
//
// Por que tambien las operaciones publicas:
//
// El enunciado pide la exigencia de rol de cada operacion, no la de cada
// operacion que tenga un rol. Una ruta publica tiene una exigencia igual de
// clara: la de no exigir nada. Declararla aqui es lo que permite que la prueba
// que recorre las rutas del Host pueda afirmar que ninguna se queda sin
// declarar. Si estas quedaran fuera, esa prueba solo podria comprobar las que
// eligiera una lista escrita a mano, que es justo lo que hay que evitar.
//
// Ojo con el Name: una entrada en este enum NO abre ni cierra nada por si
// sola. Si se declara aqui y no en RoleRequirements, la ruta se rechaza.
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

    // Gestion de permisos (RF-GP-*). Las rutas de la pieza PirryLedger.Core.Permissions
    // usan estos identificadores para declararse en RoleRequirements y para que
    // el filtro de AccessControl (que sigue siendo el mismo, por la decision de
    // PR #37) decida que nivel exigen.
    CreatePermissionRequest,
    ListOwnPermissionRequests,
    ListPendingPermissionRequests,
    ApprovePermissionRequest,
    RejectPermissionRequest,

    // Demostracion del sistema de permisos (RF-GP-05): la ruta del resumen de
    // ventas exige este permiso, no un rol. Se declara con AccessLevel.Permission
    // y el nombre del permiso vive en Requirement.Permission.
    ViewSalesSummary,

    // Operaciones del negocio (RF-NEG-02, RF-NEG-06). Las rutas viven en
    // PirryLedger.Business.Api y se declaran aqui para que la tabla siga siendo
    // el unico punto donde se lee que acceso exige cada operacion.
    IssueInvoice,
    CollectInvoicePayment,
    LeaveInvoiceAsDebt,
    EditInvoiceItems,
    MarkInvoiceAsNotDelivered,
    CancelInvoice,
    ConfirmInvoiceTransfer,
    RejectInvoiceTransfer,
    RegisterDebtPayment,
    ListInvoices,
    GetInvoice,
    ListInvoiceEdits,
}

// Los niveles de Requirement. El orden va de menos a mas: una ruta publica
// no necesita Session, una de Session acepta a cualquiera con una Session valida,
// una de Standard exige el rol de cajero, una de Administrator exige ese rol
// concreto (RD-06), y una de Permission exige tener concedida una entrada en
// per_permission_grants (RF-GP-05).
public enum AccessLevel
{
    Public,
    AnySession,
    Standard,
    Administrator,
    Permission,
}