using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Domain.Entities;

namespace PirryLedger.Core.Permissions.Application.Services;

// ADR 012: implementacion del contrato que usa el negocio para pedir un
// permiso en nombre del usuario. No recibe token: el caso de uso del negocio ya
// resolvio la sesion y pasa la identidad. Valida el catalogo y evita duplicar
// una solicitud Pending. Un nombre o un solicitante invalido es un error de
// programacion del llamante, no una peticion HTTP, por eso ArgumentException.
public sealed class PermissionRequestGateway : IPermissionRequestGateway
{
    private readonly INotificationPublisher? _notifications;
    private readonly IApplicationTransaction? _transaction;
    private readonly INotificationRecipientDirectory? _directory;

    private readonly IPermissionRequestRepository _requests;
    private readonly IPermissionAuthority _permissions;
    private readonly IClock _clock;

    public PermissionRequestGateway(
        IPermissionRequestRepository requests,
        IPermissionAuthority permissions,
        IClock clock,
        INotificationPublisher? notifications = null, IApplicationTransaction? transaction = null, INotificationRecipientDirectory? directory = null)
    {
        _requests = requests;
        _permissions = permissions;
        _clock = clock;
        _notifications = notifications;
        _transaction = transaction;
        _directory = directory;
    }

    public Task<PermissionRequestOutcome> RequestAsync(
        Guid requesterId,
        string permissionName,
        CancellationToken cancellationToken = default) =>
        _transaction is null ? RequestCoreAsync(requesterId, permissionName, cancellationToken) : _transaction.ExecuteAsync(() => RequestCoreAsync(requesterId, permissionName, cancellationToken), cancellationToken);

    private async Task<PermissionRequestOutcome> RequestCoreAsync(
        Guid requesterId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException(
                "El solicitante del permiso es obligatorio.",
                nameof(requesterId));
        }

        if (string.IsNullOrWhiteSpace(permissionName))
        {
            throw new ArgumentException(
                "El permiso solicitado es obligatorio.",
                nameof(permissionName));
        }

        if (!_permissions.IsKnown(permissionName))
        {
            throw new ArgumentException(
                "El permiso solicitado no existe.",
                nameof(permissionName));
        }

        if (await _requests.ExistsPendingAsync(requesterId, permissionName, cancellationToken))
        {
            return PermissionRequestOutcome.AlreadyPending;
        }

        var requester = _directory is null ? null : await _directory.FindActiveAsync(requesterId, cancellationToken);
        if (_directory is not null && requester is null)
            throw new ArgumentException("El solicitante no está activo.", nameof(requesterId));

        var request = PermissionRequest.Create(
            requesterId,
            permissionName,
            _clock.UtcNow, requester?.BranchId);

        await _requests.SaveAsync(request, cancellationToken);
        if (_notifications is not null)
            await _notifications.PublishAsync(new NotificationEvent(NotificationEventKind.PermissionRequested, request.Id,
                $"{requester?.Name ?? "Usuario"} solicitó {request.RequestedPermission}. Sucursal: {request.BranchId}.", request.RequesterId, request.BranchId), cancellationToken);

        return PermissionRequestOutcome.Created;
    }
}
