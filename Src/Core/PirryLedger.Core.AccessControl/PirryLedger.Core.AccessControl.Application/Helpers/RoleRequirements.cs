using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-05: el punto unico del sistema donde se lee que acceso exige cada
// Operation.
//
// La tabla esta aqui, entera, y en ningun otro sitio. Un endpoint no escribe
// "si es Administrator" ni compara roles: se declara con el Name de su
// Operation y el filtro de acceso consulta esta tabla.
//
// Por que una tabla y no un if en cada endpoint:
//
// - Si cada endpoint decidiera su acceso, la Requirement quedaria repartida en
//   varios ficheros y no se podria leer de un vistazo. El revisor tendria que
//   abrirlos todos para saber quien puede tocar la administracion de usuarios.
// - Con una tabla, cambiar "estas operaciones son de Administrator" se cambia en
//   una linea, y anadir una operation obliga a declararla. Ojo: esto NO lo
//   comprueba el compilador. Una entrada olvidada compila sin problemas. Lo que
//   la delata son dos pruebas: una recorre el enum entero y la otra recorre las
//   rutas reales del Host y exige que cada una tenga entrada aqui.
// - El rechazo sigue ocurriendo en el SERVIDOR y no en la interfaz (RD-06).
//
// Que cubra tambien las operaciones publicas es deliberado. RF-CA-05 pide "la
// Requirement de role de cada operation del sistema", y una route publica declara
// que no exige nada. Si vivieran solo las de Administrator, esta seria una tabla
// de administracion, no un punto unico del sistema, y no se podria afirmar que
// ninguna route se quedo sin declarar.
public static class RoleRequirements
{
    // La tabla. Toda la Requirement de acceso del sistema esta en estas lineas.
    //
    // El segundo campo, IsTolerant, es la unica excepcion a la regla y esta aqui
    // a proposito, no en el endpoint: Close Session tiene que responder 204
    // siempre (decision de Franklin, RF-CA-18), incluso con una credencial
    // caducada o inventada. Si el filtro exigiera una Session valida, ese endpoint
    // devolveria 401 con token caducado y 500 con token corrupto, y ademas
    // serviria de oraculo sobre si un token existo alguna vez.
    private static readonly Dictionary<Operation, Requirement> Requirements = new()
    {
        // RF-CA-01 y RF-CA-02: registro, activacion y reenvio ocurren antes de
        // que exista una Session, asi que son publicos. El reenvio responde igual
        // haya o no Email, y por eso no puede filtrar por User.
        [Operation.RegisterUser] = new(AccessLevel.Public),
        [Operation.ActivateAccount] = new(AccessLevel.Public),
        [Operation.ResendActivation] = new(AccessLevel.Public),

        // RF-CA-03: el inicio de Session es Public por definicion, es la operation
        // que devuelve la credencial. Si exigiera Session no podria usarse nunca.
        [Operation.Login] = new(AccessLevel.Public),

        // RF-CA-04: Close Session es de "cualquier Session" pero NO llega a
        // comprobar que la Session siga viva. Se responde 204 siempre, con token
        // valido, caducado o inventado, para no convertir el cierre en un
        // oraculo sobre si un token existe. De ahi IsTolerant: true.
        [Operation.Logout] = new(AccessLevel.AnySession, IsTolerant: true),

        // /me: basta con una Session valida. No es de Administrator porque un
        // Standard tambien necesita saber quien es.
        [Operation.QueryOwnSession] = new(AccessLevel.AnySession),

        // RF-CA-21: listar usuarios es de Administrator, y un Standard recibe
        // rechazo (el mismo 403 que en las otras tres).
        [Operation.ListUsers] = new(AccessLevel.Administrator),

        // RF-CA-08: cambiar el role lo hace un Administrator.
        [Operation.ChangeUserRole] = new(AccessLevel.Administrator),

        // RF-CA-20: Deactivate y Reactivate los hace un Administrator.
        [Operation.DeactivateUser] = new(AccessLevel.Administrator),
        [Operation.ReactivateUser] = new(AccessLevel.Administrator),

        [Operation.RequestRecovery] = new(AccessLevel.Public),
        [Operation.ResetPassword] = new(AccessLevel.Public),
        [Operation.ChangeOwnPassword] = new(AccessLevel.AnySession),
        [Operation.ForceReset] = new(AccessLevel.Administrator),

        // RF-GP-01..04: la pieza de permisos vive en
        // PirryLedger.Core.Permissions, pero la tabla es el unico punto del sistema
        // que decide el nivel de cada operation. El filtro (todavia en
        // PirryLedger.Core.AccessControl.Api) consulta esta tabla para esas rutas.
        [Operation.CreatePermissionRequest] = new(AccessLevel.AnySession),
        [Operation.ListOwnPermissionRequests] = new(AccessLevel.AnySession),
        [Operation.ListPendingPermissionRequests] = new(AccessLevel.Administrator),
        [Operation.ApprovePermissionRequest] = new(AccessLevel.Administrator),
        [Operation.RejectPermissionRequest] = new(AccessLevel.Administrator),

        // RF-GP-05: la ruta de demostracion exige el permiso ViewSalesSummary,
        // no un role. El filtro consulta IPermissionAuthority (inyectado via DI
        // por el Host, registrado por PirryLedger.Core.Permissions.Infrastructure).
        [Operation.ViewSalesSummary] = new(AccessLevel.Permission, Permission: PermissionName),

        // RF-NEG-02 + RF-NEG-06: quien cobra es el cajero (Standard). Emitir,
        // cobrar, dejar en deuda, editar, marcar no entregada y anular son
        // operaciones de mostrador; un Administrator recibe 403 en ellas.
        [Operation.IssueInvoice] = new(AccessLevel.Standard),
        [Operation.CollectInvoicePayment] = new(AccessLevel.Standard),
        [Operation.LeaveInvoiceAsDebt] = new(AccessLevel.Standard),
        [Operation.EditInvoiceItems] = new(AccessLevel.Standard),
        [Operation.MarkInvoiceAsNotDelivered] = new(AccessLevel.Standard),
        [Operation.CancelInvoice] = new(AccessLevel.Standard),

        // RF-NEG-02: confirmar o rechazar una transferencia, registrar un abono
        // de deuda y leer el historial de edicion son del Administrator.
        [Operation.ConfirmInvoiceTransfer] = new(AccessLevel.Administrator),
        [Operation.RejectInvoiceTransfer] = new(AccessLevel.Administrator),
        [Operation.RegisterDebtPayment] = new(AccessLevel.Administrator),
        [Operation.ListInvoiceEdits] = new(AccessLevel.Administrator),

        // Consultar facturas (RF-NEG-06): las dos rutas declaran AnySession porque
        // el conjunto de roles es cerrado (Administrator y Standard) y los dos
        // leen las mismas facturas. AnySession admite todo role existente, asi
        // que la tabla sigue diciendo a quien admite cada ruta.
        [Operation.ListInvoices] = new(AccessLevel.AnySession),
        [Operation.GetInvoice] = new(AccessLevel.AnySession),
    };

    // RF-GP-05: nombre canonico del unico permiso del catalogo. Vive aqui
    // (en lugar de en PirryLedger.Core.Permissions.Domain.PermissionCatalog)
    // para que la tabla de RoleRequirements pueda declararlo sin depender de la
    // pieza de Permissions. PermissionCatalog hace la validacion de los nombres
    // que llegan por HTTP.
    public const string PermissionName = "ViewSalesSummary";

    // La Requirement completa de una operation: que Level pide, si tolera una
    // credencial invalida y, para AccessLevel.Permission, el nombre del permiso.
    public sealed record Requirement(AccessLevel Level, bool IsTolerant = false, string? Permission = null);

    // Que acceso exige la operation. Si alguien anade una operation al enum y
    // olvida esta tabla, esto lanza al SER llamada, en vez de dejar la operation
    // abierta sin proteccion.
    public static AccessLevel RequiredLevel(Operation operation) =>
        RequirementFor(operation).Level;

    // Si la operation tolera una credencial no valida. Lo consulta el filtro para
    // no exigir una Session que el endpoint no necesita.
    public static bool ToleratesInvalidCredential(Operation operation) =>
        RequirementFor(operation).IsTolerant;

    // RF-GP-05: nombre del permiso cuando la operation exige AccessLevel.Permission.
    // Lanza si la operation no es de tipo Permission: misma regla que
    // RequiredRole para Administrator.
    public static string RequiredPermission(Operation operation) =>
        RequiredLevel(operation) switch
        {
            AccessLevel.Permission => RequirementFor(operation).Permission
                ?? throw new InvalidOperationException(
                    $"La operation {operation} exige Permission pero la entrada de RoleRequirements no declara cual."),
            _ => throw new ArgumentException(
                $"La operation {operation} no exige un Permission concreto: es {RequiredLevel(operation)}.",
                nameof(operation)),
        };

    private static Requirement RequirementFor(Operation operation)
    {
        if (!Requirements.TryGetValue(operation, out var requirement))
        {
            throw new OperationWithoutRoleRequirementException(operation);
        }

        return requirement;
    }

    // El role concreto cuando la operation exige uno. Las operaciones que no son
    // de Standard ni de Administrator no tienen role que comprobar, y preguntar
    // por el suyo seria un error de programacion, no un caso de negocio: por eso
    // lanza ArgumentException en vez de inventar un valor.
    public static Role RequiredRole(Operation operation) =>
        RequiredLevel(operation) switch
        {
            AccessLevel.Standard => Role.Standard,
            AccessLevel.Administrator => Role.Administrator,
            _ => throw new ArgumentException(
                $"La operation {operation} no exige un role concreto: es {RequiredLevel(operation)}.",
                nameof(operation)),
        };

    // Todas las operaciones declaradas. Lo usan las pruebas que verifican que la
    // tabla no tenga huecos, en los dos sentidos: cada operation del enum tiene
    // que estar aqui, y cada entrada tiene que existir en el enum.
    public static IReadOnlyCollection<Operation> DeclaredOperations() =>
        Requirements.Keys.ToArray();
}
