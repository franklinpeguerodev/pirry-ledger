using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Permissions.Application.Services;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Tests.Solicitudes;

// ADR 012 + RF-GP-01: una operacion del negocio pide el permiso en nombre del
// usuario. El gateway valida el catalogo, evita duplicar una Pending y guarda
// la solicitud. Solo una Pending bloquea; una Rejected no impide pedir otra vez.
public sealed class PruebasDeSolicitudEnNombreDelUsuario
{
    private static readonly DateTime Ahora = new(2026, 10, 31, 11, 0, 0, DateTimeKind.Utc);

    private static PermissionRequestGateway Gateway(
        FakePermissionRequestRepository requests,
        out FakePermissionAuthority authority)
    {
        authority = new FakePermissionAuthority(PermissionCatalogKnown());
        return new PermissionRequestGateway(requests, authority, new FakeClock(Ahora), directory: new Directorio());
    }

    private sealed class Directorio(Guid? branch = null, bool asignada = true) : INotificationRecipientDirectory
    {
        private readonly Guid? _branch = asignada ? branch ?? Guid.NewGuid() : null;
        public Task<NotificationRecipient?> FindActiveAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<NotificationRecipient?>(new(id, "prueba@example.invalid", false, _branch));
        public Task<IReadOnlyList<NotificationRecipient>> ListActiveAdministratorsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task EstandarSinSucursalFisicaNoGuardaSolicitud(string? value)
    {
        var requests = new FakePermissionRequestRepository();
        var directory = new Directorio(value is null ? null : Guid.Parse(value), value is not null);
        var gateway = new PermissionRequestGateway(requests, new FakePermissionAuthority(PermissionCatalogKnown()),
            new FakeClock(Ahora), directory: directory);

        await Assert.ThrowsAsync<PirryLedger.Core.Permissions.Application.Exceptions.PermissionRequestRejectedException>(
            () => gateway.RequestAsync(Guid.NewGuid(), "ViewSalesSummary"));

        Assert.Equal(0, requests.SavedCount);
    }

    private static string[] PermissionCatalogKnown() =>
        new[] { "ViewSalesSummary" };

    [Fact]
    public async Task SolicitarUnPermisoCreaUnaSolicitudPendiente()
    {
        // Arrange
        var usuarioId = Guid.NewGuid();
        var permiso = PermissionNames.CancelInvoice(Guid.NewGuid());
        var requests = new FakePermissionRequestRepository();
        var gateway = Gateway(requests, out _);

        // Act
        var resultado = await gateway.RequestAsync(usuarioId, permiso);

        // Assert
        Assert.Equal(PermissionRequestOutcome.Created, resultado);
        var solicitud = Assert.Single(await requests.ListByRequesterAsync(usuarioId));
        Assert.Equal(permiso, solicitud.RequestedPermission);
        Assert.Equal(PermissionRequestState.Pending, solicitud.State);
    }

    [Fact]
    public async Task SolicitarDosVecesElMismoPermisoDevuelveAlreadyPendingYNoDuplica()
    {
        // Arrange
        var usuarioId = Guid.NewGuid();
        var permiso = PermissionNames.CancelInvoice(Guid.NewGuid());
        var requests = new FakePermissionRequestRepository();
        var gateway = Gateway(requests, out _);

        // Act
        var primera = await gateway.RequestAsync(usuarioId, permiso);
        var segunda = await gateway.RequestAsync(usuarioId, permiso);

        // Assert
        Assert.Equal(PermissionRequestOutcome.Created, primera);
        Assert.Equal(PermissionRequestOutcome.AlreadyPending, segunda);
        Assert.Single(await requests.ListByRequesterAsync(usuarioId));
    }

    [Fact]
    public async Task LaSolicitudPendienteDeOtroUsuarioNoBloquea()
    {
        // Arrange: el permiso es el mismo, el solicitante es otro.
        var usuarioA = Guid.NewGuid();
        var usuarioB = Guid.NewGuid();
        var permiso = PermissionNames.CancelInvoice(Guid.NewGuid());
        var requests = new FakePermissionRequestRepository();
        var gateway = Gateway(requests, out _);

        // Act
        var primera = await gateway.RequestAsync(usuarioA, permiso);
        var segunda = await gateway.RequestAsync(usuarioB, permiso);

        // Assert
        Assert.Equal(PermissionRequestOutcome.Created, primera);
        Assert.Equal(PermissionRequestOutcome.Created, segunda);
        Assert.Single(await requests.ListByRequesterAsync(usuarioA));
        Assert.Single(await requests.ListByRequesterAsync(usuarioB));
    }

    [Fact]
    public async Task SolicitarUnPermisoDesconocidoSeRechaza()
    {
        // Arrange
        var requests = new FakePermissionRequestRepository();
        var gateway = Gateway(requests, out _);

        // Act + Assert: no se guarda nada.
        await Assert.ThrowsAsync<ArgumentException>(
            () => gateway.RequestAsync(Guid.NewGuid(), "PermisoInventado"));
        Assert.Equal(0, requests.SavedCount);
    }

    [Fact]
    public async Task SolicitarSinSolicitanteSeRechaza()
    {
        // Arrange
        var requests = new FakePermissionRequestRepository();
        var gateway = Gateway(requests, out _);

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => gateway.RequestAsync(Guid.Empty, PermissionNames.CancelInvoice(Guid.NewGuid())));
        Assert.Equal(0, requests.SavedCount);
    }
}
