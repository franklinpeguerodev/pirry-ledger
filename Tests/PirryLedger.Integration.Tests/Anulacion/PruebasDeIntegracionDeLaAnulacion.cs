using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Permissions.Domain.Enums;
using PirryLedger.Core.Permissions.Application.Exceptions;

namespace PirryLedger.Integration.Tests.Anulacion;

// Seccion 1.4 del enunciado: la integracion entre el negocio y Permissions,
// con las dos piezas reales. Las dos caras que exige el enunciado son estas
// dos pruebas: la anulacion se rechaza mientras la solicitud no esta aprobada,
// y se ejecuta al aprobar la solicitud.
public sealed class PruebasDeIntegracionDeLaAnulacion
{
    [Fact]
    public async Task AnularUnaFacturaPagadaSeRechazaMientrasLaSolicitudNoEstaAprobada()
    {
        // Arrange: la factura nace Paid por el camino real (emitir y cobrar).
        var escenario = new EscenarioDeIntegracion();
        var facturaId = await escenario.EmitirFacturaPagadaAsync(500m);

        // Act
        var rechazo = await Assert.ThrowsAsync<CancellationPermissionRequiredException>(
            () => escenario.AnularAsync(facturaId));

        // Assert: el rechazo dice que la solicitud se acaba de crear, la
        // factura no cambio de estado y quedo una sola solicitud Pending con el
        // nombre de esa factura.
        Assert.False(rechazo.AlreadyPending);
        Assert.Equal(InvoiceState.Paid, await escenario.EstadoDeLaFacturaAsync(facturaId));

        var solicitud = Assert.Single(await escenario.SolicitudesAsync(facturaId));
        Assert.Equal(PermissionRequestState.Pending, solicitud.State);
        Assert.Equal(escenario.Cajero.Id, solicitud.RequesterId);
        Assert.Equal(PermissionNames.CancelInvoice(facturaId), solicitud.RequestedPermission);
        Assert.Equal(0, await escenario.ConcesionesDelCajeroAsync());
    }

    [Fact]
    public async Task AprobarLaSolicitudAnulaLaFacturaEnLaMismaOperacion()
    {
        // Arrange: la factura queda Paid y su anulacion crea la solicitud.
        var escenario = new EscenarioDeIntegracion();
        var facturaId = await escenario.EmitirFacturaPagadaAsync(500m);
        await Assert.ThrowsAsync<CancellationPermissionRequiredException>(
            () => escenario.AnularAsync(facturaId));

        // Act
        await escenario.AprobarSolicitudDeAnulacionAsync(facturaId);

        // Assert
        var solicitud = Assert.Single(await escenario.SolicitudesAsync(facturaId));
        Assert.Equal(PermissionRequestState.Applied, solicitud.State);
        Assert.Equal(InvoiceState.Cancelled, await escenario.EstadoDeLaFacturaAsync(facturaId));
        Assert.Equal(0, await escenario.ConcesionesDelCajeroAsync());
    }

    [Fact]
    public async Task ElPermisoDeUnaFacturaNoHabilitaLaAnulacionDeOtra()
    {
        // Arrange: dos facturas pagadas. Solo la primera tiene permiso aprobado.
        var escenario = new EscenarioDeIntegracion();
        var primera = await escenario.EmitirFacturaPagadaAsync(500m);
        var segunda = await escenario.EmitirFacturaPagadaAsync(300m);

        await Assert.ThrowsAsync<CancellationPermissionRequiredException>(
            () => escenario.AnularAsync(primera));
        await escenario.AprobarSolicitudDeAnulacionAsync(primera);

        var solicitudDeLaPrimera = Assert.Single(await escenario.SolicitudesAsync(primera));
        Assert.Equal(PermissionNames.CancelInvoice(primera), solicitudDeLaPrimera.RequestedPermission);
        Assert.Equal(PermissionRequestState.Applied, solicitudDeLaPrimera.State);

        // Act: se intenta anular la segunda factura.
        var rechazo = await Assert.ThrowsAsync<CancellationPermissionRequiredException>(
            () => escenario.AnularAsync(segunda));

        // Assert
        Assert.False(rechazo.AlreadyPending);
        Assert.Equal(InvoiceState.Paid, await escenario.EstadoDeLaFacturaAsync(segunda));

        var solicitudDeLaSegunda = Assert.Single(await escenario.SolicitudesAsync(segunda));
        Assert.Equal(PermissionNames.CancelInvoice(segunda), solicitudDeLaSegunda.RequestedPermission);
        Assert.Equal(PermissionRequestState.Pending, solicitudDeLaSegunda.State);

        Assert.Equal(InvoiceState.Cancelled, await escenario.EstadoDeLaFacturaAsync(primera));
        Assert.Equal(0, await escenario.ConcesionesDelCajeroAsync());
    }

    [Fact]
    public async Task AprobarUnaSolicitudParaUnaFacturaInexistenteDejaLaSolicitudPendiente()
    {
        var escenario = new EscenarioDeIntegracion();
        var facturaId = Guid.NewGuid();
        await escenario.SolicitarPermisoDeAnulacionAsync(facturaId);

        await Assert.ThrowsAsync<PermissionRequestRejectedException>(
            () => escenario.AprobarSolicitudDeAnulacionAsync(facturaId));

        var solicitud = Assert.Single(await escenario.SolicitudesAsync(facturaId));
        Assert.Equal(PermissionRequestState.Pending, solicitud.State);
        Assert.Null(solicitud.ApproverId);
        Assert.Equal(0, await escenario.ConcesionesDelCajeroAsync());
    }

    [Fact]
    public async Task UnSegundoIntentoDeAnularNoDuplicaLaSolicitudPendiente()
    {
        // Arrange: una factura pagada, sin solicitud todavia.
        var escenario = new EscenarioDeIntegracion();
        var facturaId = await escenario.EmitirFacturaPagadaAsync(500m);

        // Act: dos intentos seguidos, sin aprobacion entre ellos.
        var primero = await Assert.ThrowsAsync<CancellationPermissionRequiredException>(
            () => escenario.AnularAsync(facturaId));
        var segundo = await Assert.ThrowsAsync<CancellationPermissionRequiredException>(
            () => escenario.AnularAsync(facturaId));

        // Assert: el primero crea la solicitud (403) y el segundo encuentra una
        // pendiente (409); sigue habiendo una sola y la factura no cambio.
        Assert.False(primero.AlreadyPending);
        Assert.True(segundo.AlreadyPending);
        Assert.Equal(1, await escenario.SolicitudesPendientesAsync(facturaId));
        Assert.Single(await escenario.SolicitudesAsync(facturaId));
        Assert.Equal(InvoiceState.Paid, await escenario.EstadoDeLaFacturaAsync(facturaId));
    }
}
