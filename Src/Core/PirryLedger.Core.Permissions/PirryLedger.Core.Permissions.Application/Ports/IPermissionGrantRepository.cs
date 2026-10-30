using PirryLedger.Core.Permissions.Domain.Entities;

namespace PirryLedger.Core.Permissions.Application.Ports;

// Puerto de salida para PermissionGrant (ADR 008). UserHasPermissionAsync
// vive en IPermissionAuthority y consulta con ExistsAsync; el consumo de una
// concesion (RF-NEG-06) borra la fila con ConsumeAsync. SaveAsync la consume
// el caso de uso de aprobacion.
public interface IPermissionGrantRepository
{
    Task SaveAsync(PermissionGrant grant, CancellationToken cancellationToken = default);

    // Indice unico (user_id, permission_name) en la tabla. Si ya existe,
    // SaveAsync lanza. Quien concedio por segunda vez un permiso que ya esta
    // concedido falla en la BD antes de propagarse al caso de uso.
    Task<bool> ExistsAsync(Guid userId, string permissionName, CancellationToken cancellationToken = default);

    // Borra el par (user_id, permission_name) y devuelve true si borro una
    // fila. Una segunda llamada devuelve false y no cambia nada.
    Task<bool> ConsumeAsync(Guid userId, string permissionName, CancellationToken cancellationToken = default);
}