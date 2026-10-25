using PirryLedger.Core.Permissions.Application.Exceptions;
using PirryLedger.Core.Permissions.Application.UseCases;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Tests.CasosDeUso;

// RF-GP-01: un usuario autenticado crea una solicitud de permiso y la
// solicitud nace en estado Pendiente.
//
// La suite de la pieza cubria la aprobacion, el rechazo, las transiciones
// prohibidas, la auto-resolucion y los listados, pero no la creacion: el
// criterio de aceptacion de RF-GP-01 no tenia prueba. Estas dos la cubren:
// una del camino bueno y una del camino de rechazo, que ademas comprueba que
// no queda nada guardado cuando la peticion se rechaza.
public sealed class PruebasDeCreatePermissionRequest
{
    private const string PermissionName = "ViewSalesSummary";

    [Fact]
    public async Task CrearUnaSolicitudNaceEnPendienteConSolicitanteYFecha()
    {
        // Arrange: un usuario Estandar con sesion abierta.
        var escenario = new EscenarioDePermisos();
        var token = await escenario.OpenStandardSessionAsync();
        var requests = new FakePermissionRequestRepository();
        var caso = new CreatePermissionRequest(
            requests, escenario.Authority, escenario.Authenticate, escenario.Clock);

        // Act
        var id = await caso.ExecuteAsync(token, PermissionName, CancellationToken.None);

        // Assert: la solicitud existe, nace en Pendiente y lleva el
        // solicitante y la fecha del reloj inyectado.
        var creada = await requests.FindByIdAsync(id);

        Assert.NotNull(creada);
        Assert.Equal(PermissionRequestState.Pending, creada!.State);
        Assert.Equal(escenario.Standard.Id, creada.RequesterId);
        Assert.Equal(PermissionName, creada.RequestedPermission);
        Assert.Equal(escenario.Clock.UtcNow, creada.CreatedAtUtc);

        // Todavia nadie la resolvio.
        Assert.Null(creada.ApproverId);
        Assert.Null(creada.ApprovedAtUtc);
        Assert.Null(creada.AppliedAtUtc);
        Assert.Null(creada.RejectedAtUtc);
        Assert.Null(creada.RejectionReason);

        Assert.Equal(1, requests.SavedCount);
    }

    [Fact]
    public async Task CrearUnPermisoFueraDelCatalogoNoGuardaNada()
    {
        // Arrange
        var escenario = new EscenarioDePermisos();
        var token = await escenario.OpenStandardSessionAsync();
        var requests = new FakePermissionRequestRepository();
        var caso = new CreatePermissionRequest(
            requests, escenario.Authority, escenario.Authenticate, escenario.Clock);

        // Act + Assert: un permiso que no esta en el catalogo se rechaza y no
        // queda ninguna solicitud guardada.
        var error = await Assert.ThrowsAsync<PermissionRequestRejectedException>(
            () => caso.ExecuteAsync(token, "PermisoInventado", CancellationToken.None));

        Assert.Equal("El permiso solicitado no existe.", error.Message);
        Assert.Equal(0, requests.SavedCount);
    }
}
