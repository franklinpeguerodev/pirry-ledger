using PirryLedger.Core.Permissions.Domain.Entities;

namespace PirryLedger.Core.Permissions.Application.Ports;

// Puerto de salida para PermissionGrant (ADR 008). Su unica consulta
// externa hoy es UserHasPermissionAsync, que vive en IPermissionAuthority.
// SaveAsync la consume el caso de uso de aprobacion, en el siguiente PR.
public interface IPermissionGrantRepository
{
    Task SaveAsync(PermissionGrant grant, CancellationToken cancellationToken = default);

    // Indice unico (user_id, permission_name) en la tabla. Si ya existe,
    // SaveAsync lanza. Quien concedio por segunda vez un permiso que ya esta
    // concedido falla en la BD antes de propagarse al caso de uso.
    Task<bool> ExistsAsync(Guid userId, string permissionName, CancellationToken cancellationToken = default);
}