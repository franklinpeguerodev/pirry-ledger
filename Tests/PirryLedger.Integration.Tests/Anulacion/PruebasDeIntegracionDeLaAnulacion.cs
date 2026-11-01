using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Integration.Tests.Anulacion;

// Seccion 1.4 del enunciado: la integracion entre el negocio y Permissions,
// con las dos piezas reales. Las dos caras que exige el enunciado son estas
// dos pruebas: la anulacion se rechaza mientras la solicitud no esta aprobada,
// y se permite despues de que un Administrator la apruebe.
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
    public async Task UnAdministradorApruebaLaSolicitudYEntoncesLaAnulacionSePermite()
    {
        // Arrange: la factura queda Paid y su anulacion crea la solicitud.
        var escenario = new EscenarioDeIntegracion();
        var facturaId = await escenario.EmitirFacturaPagadaAsync(500m);
        await Assert.ThrowsAsync<CancellationPermissionRequiredException>(
            () => escenario.AnularAsync(facturaId));

        // Act: el Administrator real aprueba la solicitud y el cajero vuelve a
        // intentar la anulacion.
        await escenario.AprobarSolicitudDeAnulacionAsync(facturaId);
        Assert.Equal(1, await escenario.ConcesionesDelCajeroAsync());

        var factura = await escenario.AnularAsync(facturaId);

        // Assert: la anulacion paso y la concesion se consumio al usarla.
        Assert.Equal(InvoiceState.Cancelled, factura.State);
        Assert.Equal(InvoiceState.Cancelled, await escenario.EstadoDeLaFacturaAsync(facturaId));
        Assert.Equal(0, await escenario.ConcesionesDelCajeroAsync());
    }
}
