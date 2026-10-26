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
    private readonly IPermissionRequestRepository _requests;
    private readonly Authenticate _authenticate;
    private readonly IClock _clock;

    public RejectPermissionRequest(
        IPermissionRequestRepository requests,
        Authenticate authenticate,
        IClock clock)
    {
        _requests = requests;
        _authenticate = authenticate;
        _clock = clock;
    }

    public async Task ExecuteAsync(
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
        request.Reject(caller.Id, _clock.UtcNow, reason);

        await _requests.SaveAsync(request, cancellationToken);
    }
}