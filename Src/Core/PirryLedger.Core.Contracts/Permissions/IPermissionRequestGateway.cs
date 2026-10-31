namespace PirryLedger.Core.Contracts.Permissions;

// ADR 012: una operacion del negocio pide un permiso en nombre del usuario, sin
// reutilizar el endpoint HTTP de solicitudes. Business.Application solo puede
// referenciar Core.Contracts, asi que el contrato vive aqui y la pieza de
// Permissions lo implementa. El caso de uso del negocio ya tiene la identidad
// del llamante: no se pasa un token.
public interface IPermissionRequestGateway
{
    // Crea una solicitud Pending para (requesterId, permissionName) o informa
    // de que ya hay una. Solo una Pending bloquea: una Rejected o una ya
    // consumida no impiden pedir el permiso otra vez.
    Task<PermissionRequestOutcome> RequestAsync(
        Guid requesterId,
        string permissionName,
        CancellationToken cancellationToken = default);
}
