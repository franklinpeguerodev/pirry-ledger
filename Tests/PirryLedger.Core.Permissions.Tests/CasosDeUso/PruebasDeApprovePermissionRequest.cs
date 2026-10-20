using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Permissions.Application.Exceptions;
using PirryLedger.Core.Permissions.Application.UseCases;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Tests.CasosDeUso;

// RF-GP-03 + RF-GP-05 + RF-GP-06 + RF-GP-07: aprobar pasa por tres mutaciones
// en una sola operacion (Pending -> Approved -> Applied + INSERT en
// per_permission_grants). La regla de auto-resolucion (RF-GP-07) compara
// identidades (Id), no roles.
public sealed class PruebasDeApprovePermissionRequest
{
    private const string PermissionName = "ViewSalesSummary";

    // Helper: escenario + repositorios + caso de uso de aprobacion.
    private static async Task<(EscenarioDePermisos escenario, FakePermissionRequestRepository requests, FakePermissionGrantRepository grants, ApprovePermissionRequest caso, string standardToken, string adminToken)>
        BuildAsync()
    {
        var escenario = new EscenarioDePermisos();
        var standardToken = await escenario.OpenStandardSessionAsync();
        var adminToken = await escenario.OpenAdminSessionAsync();
        var requests = new FakePermissionRequestRepository();
        var grants = new FakePermissionGrantRepository();
        var caso = new ApprovePermissionRequest(requests, grants, escenario.Authenticate, escenario.Clock);
        return (escenario, requests, grants, caso, standardToken, adminToken);
    }

    private static async Task<Guid> CreatePendingAsync(
        EscenarioDePermisos escenario,
        FakePermissionRequestRepository requests,
        string standardToken)
    {
        var crear = new CreatePermissionRequest(
            requests, escenario.Authority, escenario.Authenticate, escenario.Clock);
        return await crear.ExecuteAsync(standardToken, PermissionName, CancellationToken.None);
    }

    [Fact]
    public async Task AprobarUnaSolicitudPendienteLaPasaAAplicadaYRegistraConcesion()
    {
        // Arrange: el Standard crea, el Admin aprueba.
        var (escenario, requests, grants, caso, standardToken, adminToken) = await BuildAsync();
        var requestId = await CreatePendingAsync(escenario, requests, standardToken);

        // Act
        await caso.ExecuteAsync(adminToken, requestId, CancellationToken.None);

        // Assert
        var aprobada = await requests.FindByIdAsync(requestId);
        Assert.NotNull(aprobada);
        Assert.Equal(PermissionRequestState.Applied, aprobada!.State);
        Assert.Equal(escenario.Admin.Id, aprobada.ApproverId);
        Assert.NotNull(aprobada.ApprovedAtUtc);
        Assert.NotNull(aprobada.AppliedAtUtc);

        // RF-GP-05 + ADR 008: la fila en per_permission_grants existe.
        Assert.True(await grants.ExistsAsync(escenario.Standard.Id, PermissionName));
        Assert.Equal(1, grants.SavedCount);
    }

    [Fact]
    public async Task AprobarUnaSolicitudYaAprobadaNoCambiaElEstado()
    {
        // Arrange: el Standard crea, el Admin aprueba una vez. La segunda
        // aprobacion tiene que fallar (RF-GP-06: transicion prohibida).
        var (escenario, requests, grants, caso, standardToken, adminToken) = await BuildAsync();
        var requestId = await CreatePendingAsync(escenario, requests, standardToken);

        await caso.ExecuteAsync(adminToken, requestId, CancellationToken.None);

        // Act + Assert: una segunda aprobacion lanza porque el estado ya
        // no es Pending. El estado se queda donde esta (Applied); no hay
        // segundo INSERT en per_permission_grants.
        await Assert.ThrowsAsync<InvalidPermissionRequestTransitionApplicationException>(
            () => caso.ExecuteAsync(adminToken, requestId, CancellationToken.None));

        var despues = await requests.FindByIdAsync(requestId);
        Assert.Equal(PermissionRequestState.Applied, despues!.State);
        Assert.Equal(1, grants.SavedCount);
    }

    [Fact]
    public async Task AprobarUnaSolicitudInexistenteSeRechaza()
    {
        // Arrange
        var (escenario, requests, grants, caso, _, adminToken) = await BuildAsync();
        var inexistente = Guid.NewGuid();

        // Act + Assert
        await Assert.ThrowsAsync<PermissionRequestRejectedException>(
            () => caso.ExecuteAsync(adminToken, inexistente, CancellationToken.None));
        Assert.Equal(0, grants.SavedCount);
    }

    [Fact]
    public async Task UnAdministradorNoPuedeAprobarSuPropiaSolicitud()
    {
        // Arrange: el Admin crea la solicitud y luego intenta aprobarla.
        // Esta es la version "directa" de RF-GP-07: la misma persona
        // intenta resolver su propia solicitud.
        var (escenario, requests, grants, caso, _, adminToken) = await BuildAsync();
        var requestId = await CreatePendingAsync(escenario, requests, adminToken);

        // Act + Assert: la regla compara identidades (admin.Id == request.RequesterId).
        await Assert.ThrowsAsync<SelfResolutionRejectedException>(
            () => caso.ExecuteAsync(adminToken, requestId, CancellationToken.None));

        var sinTocar = await requests.FindByIdAsync(requestId);
        Assert.Equal(PermissionRequestState.Pending, sinTocar!.State);
        Assert.Null(sinTocar.ApproverId);
        Assert.Equal(0, grants.SavedCount);
    }

    [Fact]
    public async Task UnEstandarAscendidoNoPuedeAprobarSuPropiaSolicitud()
    {
        // Arrange: el camino "realista" de RF-GP-07. Un Standard crea, el
        // Admin lo asciende (lo que invalida su sesion por ADR 009), el
        // Standard re-inicia sesion, ya como Administrator, intenta aprobar
        // su propia solicitud y la regla lo rechaza.
        var escenario = new EscenarioDePermisos();
        var standardToken = await escenario.OpenStandardSessionAsync();
        var adminToken = await escenario.OpenAdminSessionAsync();
        var requests = new FakePermissionRequestRepository();
        var grants = new FakePermissionGrantRepository();
        var caso = new ApprovePermissionRequest(requests, grants, escenario.Authenticate, escenario.Clock);

        // El Standard crea la solicitud.
        var crear = new CreatePermissionRequest(requests, escenario.Authority, escenario.Authenticate, escenario.Clock);
        var requestId = await crear.ExecuteAsync(standardToken, PermissionName, CancellationToken.None);

        // El Admin asciende al Standard: cambia el rol e invalida la sesion
        // (ADR 009). User.ChangeRole incrementa CredentialVersion.
        var standard = escenario.Standard;
        standard.ChangeRole(PirryLedger.Core.AccessControl.Domain.Enums.Role.Administrator);

        // El Standard re-inicia sesion con el rol nuevo.
        var newStandardToken = await escenario.OpenStandardSessionAsync();

        // Act + Assert: ya es Administrator, pero su Id es el mismo que el
        // solicitante de la solicitud. La regla lo rechaza por identidad.
        await Assert.ThrowsAsync<SelfResolutionRejectedException>(
            () => caso.ExecuteAsync(newStandardToken, requestId, CancellationToken.None));

        var sinTocar = await requests.FindByIdAsync(requestId);
        Assert.Equal(PermissionRequestState.Pending, sinTocar!.State);
        Assert.Equal(0, grants.SavedCount);
    }
}