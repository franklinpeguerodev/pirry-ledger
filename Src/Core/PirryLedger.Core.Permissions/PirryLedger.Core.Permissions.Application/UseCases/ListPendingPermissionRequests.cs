using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Application.UseCases;

// RF-GP-02 (caso del rol): un Administrator lista todas las solicitudes
// pendientes. La proteccion de role la hace el filtro de AccessControl
// declarando la operation con AccessLevel.Administrator; este caso de uso
// no la repite, igual que ListUsers de AccessControl.
public sealed class ListPendingPermissionRequests
{
    private readonly IPermissionRequestRepository _requests;
    private readonly Authenticate _authenticate;

    public ListPendingPermissionRequests(IPermissionRequestRepository requests, Authenticate authenticate)
    {
        _requests = requests;
        _authenticate = authenticate;
    }

    public async Task<IReadOnlyList<ListedPermissionRequest>> ExecuteAsync(
        string plainToken,
        CancellationToken cancellationToken = default)
    {
        // La authorization de role la aplica el filtro antes de llegar aqui.
        // Aun asi, el caso de uso llama a Authenticate.ExecuteAsync para
        // obtener el caller (no leeria de RoleRequirements; lo lee del
        // contrato). El filtro es quien dice si llega aqui.
        _ = await _authenticate.ExecuteAsync(plainToken, cancellationToken);

        var requests = await _requests.ListByStateAsync(PermissionRequestState.Pending, cancellationToken);

        return requests.Select(PermissionRequestMapping.ToListed).ToList();
    }
}