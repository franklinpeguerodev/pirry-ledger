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
    private readonly IPermissionRequestRepository _requests;
    private readonly IPermissionAuthority _permissions;
    private readonly IClock _clock;

    public PermissionRequestGateway(
        IPermissionRequestRepository requests,
        IPermissionAuthority permissions,
        IClock clock)
    {
        _requests = requests;
        _permissions = permissions;
        _clock = clock;
    }

    public async Task<PermissionRequestOutcome> RequestAsync(
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

        var request = PermissionRequest.Create(
            requesterId,
            permissionName,
            _clock.UtcNow);

        await _requests.SaveAsync(request, cancellationToken);

        return PermissionRequestOutcome.Created;
    }
}
