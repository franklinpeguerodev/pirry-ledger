using PirryLedger.Core.Permissions.Application.Exceptions;
using PirryLedger.Core.Permissions.Application.UseCases;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Tests.CasosDeUso;

// RF-GP-04: rechazar exige motivo escrito no vacio. RF-GP-07: la regla de
// auto-resolucion aplica tambien al rechazo (no resuelve su propia
// solicitud). RF-GP-06: solo se puede rechazar una solicitud en Pending.
public sealed class PruebasDeRejectPermissionRequest
{
    private const string PermissionName = "ViewSalesSummary";

    [Fact]
    public async Task RechazarUnaSolicitudPendienteLaPasaARechazadaConMotivo()
    {
        // Arrange
        var escenario = new EscenarioDePermisos();
        var standardToken = await escenario.OpenStandardSessionAsync();
        var adminToken = await escenario.OpenAdminSessionAsync();
        var requests = new FakePermissionRequestRepository();
        var grants = new FakePermissionGrantRepository();
        var crear = new CreatePermissionRequest(requests, escenario.Authority, escenario.Authenticate, escenario.Clock);
        var rechazar = new RejectPermissionRequest(requests, escenario.Authenticate, escenario.Clock);

        var requestId = await crear.ExecuteAsync(standardToken, PermissionName, CancellationToken.None);

        // Act
        await rechazar.ExecuteAsync(adminToken, requestId, "No cumple los requisitos.", CancellationToken.None);

        // Assert
        var rechazada = await requests.FindByIdAsync(requestId);
        Assert.NotNull(rechazada);
        Assert.Equal(PermissionRequestState.Rejected, rechazada!.State);
        Assert.Equal(escenario.Admin.Id, rechazada.ApproverId);
        Assert.Equal("No cumple los requisitos.", rechazada.RejectionReason);
        Assert.NotNull(rechazada.RejectedAtUtc);

        // Rechazar NO crea una fila en per_permission_grants (a diferencia
        // de aprobar, que si la crea por RF-GP-05 + ADR 008).
        Assert.Equal(0, grants.SavedCount);
    }

    [Fact]
    public async Task RechazarSinMotivoSeRechaza()
    {
        // Arrange
        var escenario = new EscenarioDePermisos();
        var standardToken = await escenario.OpenStandardSessionAsync();
        var adminToken = await escenario.OpenAdminSessionAsync();
        var requests = new FakePermissionRequestRepository();
        var grants = new FakePermissionGrantRepository();
        var crear = new CreatePermissionRequest(requests, escenario.Authority, escenario.Authenticate, escenario.Clock);
        var rechazar = new RejectPermissionRequest(requests, escenario.Authenticate, escenario.Clock);

        var requestId = await crear.ExecuteAsync(standardToken, PermissionName, CancellationToken.None);

        // Act + Assert: motivo vacio lanza antes de tocar la solicitud.
        await Assert.ThrowsAsync<PermissionRequestRejectedException>(
            () => rechazar.ExecuteAsync(adminToken, requestId, "   ", CancellationToken.None));

        var sinTocar = await requests.FindByIdAsync(requestId);
        Assert.Equal(PermissionRequestState.Pending, sinTocar!.State);
        Assert.Null(sinTocar.RejectionReason);
    }

    [Fact]
    public async Task UnAdministradorNoPuedeRechazarSuPropiaSolicitud()
    {
        // Arrange: la misma regla que aprobar (RF-GP-07). El Admin crea
        // su propia solicitud e intenta rechazarla: la identidad coincide
        // y se rechaza.
        var escenario = new EscenarioDePermisos();
        var adminToken = await escenario.OpenAdminSessionAsync();
        var requests = new FakePermissionRequestRepository();
        var grants = new FakePermissionGrantRepository();
        var crear = new CreatePermissionRequest(requests, escenario.Authority, escenario.Authenticate, escenario.Clock);
        var rechazar = new RejectPermissionRequest(requests, escenario.Authenticate, escenario.Clock);

        var requestId = await crear.ExecuteAsync(adminToken, PermissionName, CancellationToken.None);

        await Assert.ThrowsAsync<SelfResolutionRejectedException>(
            () => rechazar.ExecuteAsync(adminToken, requestId, "Cualquier motivo", CancellationToken.None));

        var sinTocar = await requests.FindByIdAsync(requestId);
        Assert.Equal(PermissionRequestState.Pending, sinTocar!.State);
    }
}