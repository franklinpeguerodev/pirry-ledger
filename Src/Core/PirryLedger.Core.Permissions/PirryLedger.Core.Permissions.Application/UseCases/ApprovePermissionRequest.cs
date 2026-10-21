using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Permissions.Application.Exceptions;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Domain.Entities;

namespace PirryLedger.Core.Permissions.Application.UseCases;

// RF-GP-03 + RF-GP-05 + RF-GP-07: aprobar una solicitud pendiente pasa por
// tres transiciones en una sola operacion (misma transaccion EF Core):
//
//   1. Pending -> Approved: graba aprobador e instante UTC.
//   2. Approved -> Applied: graba el instante de aplicacion.
//   3. INSERT en per_permission_grants: registra la concesion.
//
// La regla de RF-GP-07 (no resolver tu propia solicitud) se compara por
// identidad (Id del solicitante contra Id del resolutor), no por role, tal
// como exige current-iteration.md punto 2.
public sealed class ApprovePermissionRequest
{
    private readonly IPermissionRequestRepository _requests;
    private readonly IPermissionGrantRepository _grants;
    private readonly Authenticate _authenticate;
    private readonly IClock _clock;

    public ApprovePermissionRequest(
        IPermissionRequestRepository requests,
        IPermissionGrantRepository grants,
        Authenticate authenticate,
        IClock clock)
    {
        _requests = requests;
        _grants = grants;
        _authenticate = authenticate;
        _clock = clock;
    }

    public async Task ExecuteAsync(
        string plainToken,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        // La authorization de role la aplica el filtro antes de llegar aqui.
        // Aun asi, Authenticate es quien resuelve el token: nos da el caller
        // y garantiza que la sesion es valida.
        var caller = await _authenticate.ExecuteWithRoleAsync(
            plainToken,
            RoleRequirements.RequiredRole(Operation.ApprovePermissionRequest),
            cancellationToken);

        var request = await _requests.FindByIdAsync(requestId, cancellationToken);

        if (request is null)
        {
            throw new PermissionRequestRejectedException("La solicitud indicada no existe.");
        }

        // RF-GP-06: aprobar dos veces la misma solicitud falla por la maquina
        // de estados. La regla vive en PermissionRequest.Approve.
        if (request.State != Domain.Enums.PermissionRequestState.Pending)
        {
            throw new InvalidPermissionRequestTransitionApplicationException(
                "Solo se pueden aprobar solicitudes en estado Pendiente.");
        }

        // RF-GP-07: nadie resuelve su propia solicitud. Compara identidades,
        // no roles: si el caller tiene el rol Administrator (el filtro lo
        // confirma) pero su Id coincide con el del solicitante, se rechaza.
        if (request.RequesterId == caller.Id)
        {
            throw new SelfResolutionRejectedException();
        }

        var now = _clock.UtcNow;

        // RF-GP-03: aprobar registra aprobador e instante UTC.
        request.Approve(caller.Id, now);

        // RF-GP-05: aplicar ocurre en la misma operacion. Si algo falla a
        // partir de aqui, la transicion EF Core hace rollback y la solicitud
        // sigue en Pending sin aprobador ni permiso concedido.
        request.MarkApplied(now);

        // RF-GP-05 + ADR 008: el INSERT de la concesion vive en la misma
        // transaccion. La pieza de Business que registre auditorias futuras
        // (semana 14) vera el par (granted_at_utc, granted_by_user_id) ya
        // rellenado.
        var grant = PermissionGrant.Create(
            request.RequesterId,
            request.RequestedPermission,
            now,
            caller.Id);

        await _grants.SaveAsync(grant, cancellationToken);
        await _requests.SaveAsync(request, cancellationToken);
    }
}