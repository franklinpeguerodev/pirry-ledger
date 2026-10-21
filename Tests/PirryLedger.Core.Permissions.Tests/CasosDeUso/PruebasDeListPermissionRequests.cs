using PirryLedger.Core.Permissions.Application.UseCases;

namespace PirryLedger.Core.Permissions.Tests.CasosDeUso;

// RF-GP-02: un usuario ve solo sus propias solicitudes; un Administrator
// ve las solicitudes pendientes globales. La proteccion de rol para
// /pending vive en el filtro (Operation.ListPendingPermissionRequests =
// AccessLevel.Administrator); el caso de uso solo lista.
public sealed class PruebasDeListPermissionRequests
{
    private const string PermissionName = "ViewSalesSummary";

    [Fact]
    public async Task UnEstandarSoloVeSusPropiasSolicitudes()
    {
        // Arrange: escenario con Standard y Admin, ambos crean solicitudes.
        var escenario = new EscenarioDePermisos();
        var standardToken = await escenario.OpenStandardSessionAsync();
        var adminToken = await escenario.OpenAdminSessionAsync();
        var requests = new FakePermissionRequestRepository();
        var crear = new CreatePermissionRequest(requests, escenario.Authority, escenario.Authenticate, escenario.Clock);

        await crear.ExecuteAsync(standardToken, PermissionName, CancellationToken.None);
        await crear.ExecuteAsync(standardToken, PermissionName, CancellationToken.None);
        await crear.ExecuteAsync(adminToken, PermissionName, CancellationToken.None);

        // Act
        var caso = new ListOwnPermissionRequests(requests, escenario.Authenticate);
        var propias = await caso.ExecuteAsync(standardToken, CancellationToken.None);

        // Assert: el Standard solo ve las suyas, no la del Admin.
        Assert.Equal(2, propias.Count);
        Assert.All(propias, r => Assert.Equal(escenario.Standard.Id, r.RequesterId));
    }

    [Fact]
    public async Task ListarPendientesDevuelveSoloLasQueEstanEnPending()
    {
        // Arrange: dos solicitudes, una aprobada y otra pendiente.
        var escenario = new EscenarioDePermisos();
        var standardToken = await escenario.OpenStandardSessionAsync();
        var adminToken = await escenario.OpenAdminSessionAsync();
        var requests = new FakePermissionRequestRepository();
        var grants = new FakePermissionGrantRepository();
        var crear = new CreatePermissionRequest(requests, escenario.Authority, escenario.Authenticate, escenario.Clock);
        var aprobar = new ApprovePermissionRequest(requests, grants, escenario.Authenticate, escenario.Clock);

        var primera = await crear.ExecuteAsync(standardToken, PermissionName, CancellationToken.None);
        await crear.ExecuteAsync(standardToken, PermissionName, CancellationToken.None);
        await aprobar.ExecuteAsync(adminToken, primera, CancellationToken.None);

        // Act
        var caso = new ListPendingPermissionRequests(requests, escenario.Authenticate);
        var pendientes = await caso.ExecuteAsync(adminToken, CancellationToken.None);

        // Assert: solo la segunda aparece en pendientes.
        Assert.Single(pendientes);
        Assert.NotEqual(primera, pendientes[0].Id);
    }
}