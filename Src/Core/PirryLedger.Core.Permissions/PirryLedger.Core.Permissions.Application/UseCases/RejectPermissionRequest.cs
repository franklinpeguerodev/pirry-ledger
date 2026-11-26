using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Permissions.Application.Exceptions;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Domain.Entities;

namespace PirryLedger.Core.Permissions.Application.UseCases;

// RF-GP-04 + RF-GP-07: rechazar una solicitud pendiente exige motivo
// escrito (no vacio) y compara identidades para impedir la auto-resolucion,
// igual que ApprovePermissionRequest. A diferencia de la aprobacion, no
// hay transicion a Applied ni INSERT en per_permission_grants: la solicitud
// muere en Rejected.
public sealed class RejectPermissionRequest
{
    private readonly INotificationPublisher? _notifications;
    private readonly IApplicationTransaction? _transaction;

    private readonly IPermissionRequestRepository _requests;
    private readonly Authenticate _authenticate;
    private readonly IClock _clock;

    public RejectPermissionRequest(
        IPermissionRequestRepository requests,
        Authenticate authenticate,
        IClock clock,
        INotificationPublisher? notifications = null, IApplicationTransaction? transaction = null)
    {
        _requests = requests;
        _authenticate = authenticate;
        _clock = clock;
        _notifications = notifications;
        _transaction = transaction;
    }

    public async Task ExecuteAsync(
        string plainToken,
        Guid requestId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (_transaction is null) await ExecuteCoreAsync(plainToken, requestId, reason, cancellationToken);
        else await _transaction.ExecuteAsync(async () => { await ExecuteCoreAsync(plainToken, requestId, reason, cancellationToken); return 0; }, cancellationToken);
    }

    private async Task ExecuteCoreAsync(
        string plainToken,
        Guid requestId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        // RF-GP-04: el motivo es obligatorio y no vacio.
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new PermissionRequestRejectedException("El motivo del rechazo es obligatorio.");
        }

        var caller = await _authenticate.ExecuteWithRoleAsync(
            plainToken,
            RoleRequirements.RequiredRole(Operation.RejectPermissionRequest),
            cancellationToken);

        var request = await _requests.FindByIdAsync(requestId, cancellationToken);

        if (request is null)
        {
            throw new PermissionRequestRejectedException("La solicitud indicada no existe.");
        }

        if (request.State != Domain.Enums.PermissionRequestState.Pending)
        {
            throw new InvalidPermissionRequestTransitionApplicationException(
                "Solo se pueden rechazar solicitudes en estado Pendiente.");
        }

        // RF-GP-07: misma regla que ApprovePermissionRequest.
        if (request.RequesterId == caller.Id)
        {
            throw new SelfResolutionRejectedException();
        }

        request.Reject(caller.Id, _clock.UtcNow, reason);

        await _requests.SaveAsync(request, cancellationToken);
        if (_notifications is not null)
            await _notifications.PublishAsync(new NotificationEvent(NotificationEventKind.PermissionRejected, request.Id,
                $"Su solicitud de {request.RequestedPermission} fue rechazada. Motivo: {request.RejectionReason}.", request.RequesterId, request.BranchId), cancellationToken);
    }
}