using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Permissions.Application.Ports;

namespace PirryLedger.Core.Permissions.Application.UseCases;

// RF-GP-02: un usuario autenticado lista sus propias solicitudes. El
// filtro por RequesterId vive aqui, en el caso de uso, no en el endpoint
// (mismo patron que ListUsers de AccessControl: la proyeccion al record
// de salida va aqui tambien).
public sealed class ListOwnPermissionRequests
{
    private readonly IPermissionRequestRepository _requests;
    private readonly Authenticate _authenticate;

    public ListOwnPermissionRequests(IPermissionRequestRepository requests, Authenticate authenticate)
    {
        _requests = requests;
        _authenticate = authenticate;
    }

    public async Task<IReadOnlyList<ListedPermissionRequest>> ExecuteAsync(
        string plainToken,
        CancellationToken cancellationToken = default)
    {
        var caller = await _authenticate.ExecuteAsync(plainToken, cancellationToken);

        var requests = await _requests.ListByRequesterAsync(caller.Id, cancellationToken);

        return requests.Select(PermissionRequestMapping.ToListed).ToList();
    }
}