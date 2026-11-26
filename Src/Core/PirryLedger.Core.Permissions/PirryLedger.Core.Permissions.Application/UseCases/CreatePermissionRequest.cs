using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Permissions.Application.Exceptions;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Domain;
using PirryLedger.Core.Permissions.Domain.Entities;

namespace PirryLedger.Core.Permissions.Application.UseCases;

// RF-GP-01: un usuario autenticado (Estandar o Administrator) crea una
// solicitud de permiso. La solicitud nace en Pending y queda a la espera
// de que un Administrator la apruebe o rechace.
//
// El validador de la politica de contrasenas (PasswordPolicy) vive en
// AccessControl; la pieza de Permissions no la duplica. Aqui solo se
// valida el nombre del permiso contra el catalogo (RF-GP-01).
public sealed class CreatePermissionRequest
{
    private readonly INotificationPublisher? _notifications;
    private readonly IApplicationTransaction? _transaction;

    private readonly IPermissionRequestRepository _requests;
    private readonly IPermissionAuthority _permissions;
    private readonly Authenticate _authenticate;
    private readonly IClock _clock;

    public CreatePermissionRequest(
        IPermissionRequestRepository requests,
        IPermissionAuthority permissions,
        Authenticate authenticate,
        IClock clock,
        INotificationPublisher? notifications = null, IApplicationTransaction? transaction = null)
    {
        _requests = requests;
        _permissions = permissions;
        _authenticate = authenticate;
        _clock = clock;
        _notifications = notifications;
        _transaction = transaction;
    }

    // Devuelve el id de la solicitud creada para que el endpoint pueda
    // responder 202 Accepted con Location al recurso. La solicitud todavia
    // esta en Pending: aprobar y aplicar ocurren en otra operacion.
    public Task<Guid> ExecuteAsync(
        string plainToken,
        string requestedPermission,
        CancellationToken cancellationToken = default) =>
        _transaction is null ? ExecuteCoreAsync(plainToken, requestedPermission, cancellationToken) : _transaction.ExecuteAsync(() => ExecuteCoreAsync(plainToken, requestedPermission, cancellationToken), cancellationToken);

    private async Task<Guid> ExecuteCoreAsync(
        string plainToken,
        string requestedPermission,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestedPermission))
        {
            throw new PermissionRequestRejectedException("El permiso solicitado es obligatorio.");
        }

        // RF-CA-04: el permiso llega al caso de uso autenticado. La regla
        // "no pidas un permiso que no existe" se valida contra el catalogo
        // antes de crear la solicitud, para no tener que limpiar filas
        // despues.
        var caller = await _authenticate.ExecuteAsync(plainToken, cancellationToken);

        if (!_permissions.IsKnown(requestedPermission))
        {
            throw new PermissionRequestRejectedException("El permiso solicitado no existe.");
        }

        var request = PermissionRequest.Create(
            caller.Id,
            requestedPermission,
            _clock.UtcNow, PermissionRequestBranch.Resolve(caller.Role == PirryLedger.Core.AccessControl.Domain.Enums.Role.Administrator, caller.BranchId));

        await _requests.SaveAsync(request, cancellationToken);
        if (_notifications is not null)
            await _notifications.PublishAsync(new NotificationEvent(NotificationEventKind.PermissionRequested, request.Id,
                $"{caller.Name} solicitó {request.RequestedPermission}. Sucursal: {request.BranchId}.", request.RequesterId, request.BranchId), cancellationToken);

        return request.Id;
    }
}