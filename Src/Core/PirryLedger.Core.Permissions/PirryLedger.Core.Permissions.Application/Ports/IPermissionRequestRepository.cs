using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Application.Ports;

// Puerto de salida para la entidad PermissionRequest. La implementacion vive
// en Infrastructure (EF Core sobre per_permission_requests). Domain y
// Application no saben que existe una base de datos.
public interface IPermissionRequestRepository
{
    Task<PermissionRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(PermissionRequest request, CancellationToken cancellationToken = default);

    // RF-GP-02: las solicitudes pendientes para que el Administrator las liste.
    // El filtro por estado se hace en SQL para no traer todas las filas.
    Task<IReadOnlyList<PermissionRequest>> ListByStateAsync(PermissionRequestState state, CancellationToken cancellationToken = default);

    // RF-GP-02: las solicitudes de un solicitante concreto, para que vea solo
    // las suyas en /mine.
    Task<IReadOnlyList<PermissionRequest>> ListByRequesterAsync(Guid requesterId, CancellationToken cancellationToken = default);

    // ADR 012: evita duplicar una solicitud Pending cuando el negocio pide el
    // permiso en nombre del usuario. Solo Pending bloquea.
    Task<bool> ExistsPendingAsync(
        Guid requesterId,
        string permissionName,
        CancellationToken cancellationToken = default);
}