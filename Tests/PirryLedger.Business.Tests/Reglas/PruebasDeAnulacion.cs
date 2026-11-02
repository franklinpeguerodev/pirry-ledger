using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Tests.Reglas;

// RF-NEG-02 + RF-NEG-03: anular una factura. La tabla permite Issued, Paid e
// InDebt -> Cancelled; el resto de estados no puede anularse. La consulta del
// permiso no vive aqui: depende de la factura y la resuelve el caso de uso.
public sealed class PruebasDeAnulacion
{
    private static readonly DateTime Ahora = new(2026, 10, 31, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Cajero = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Invoice FacturaEmitida(bool isDelivery = false)
    {
        var factura = Invoice.Create();
        factura.Issue(new[] { InvoiceItem.Create("Combo", 500m, 1) }, isDelivery, Ahora);
        return factura;
    }

    private static Invoice FacturaPagada()
    {
        var factura = FacturaEmitida();
        factura.Collect(new[] { InvoicePayment.Cash(500m, 500m, Ahora, Cajero) });
        return factura;
    }

    private static Invoice FacturaEnDeuda()
    {
        var factura = FacturaEmitida();
        factura.LeaveAsDebt("Ana Perez", payments: null);
        return factura;
    }

    private static Invoice FacturaEnVerificacionPendiente()
    {
        var factura = FacturaEmitida();
        factura.Collect(new[] { InvoicePayment.Transfer(500m, Ahora, Cajero) });
        return factura;
    }

    private static Invoice FacturaNoEntregada()
    {
        var factura = FacturaEmitida(isDelivery: true);
        factura.MarkAsNotDelivered();
        return factura;
    }

    [Fact]
    public void AnularUnaFacturaEmitidaPasaAAnulada()
    {
        // Arrange: Issued -> Cancelled no exige permiso (decision 3).
        var factura = FacturaEmitida();

        // Act
        factura.Cancel();

        // Assert
        Assert.Equal(InvoiceState.Cancelled, factura.State);
    }

    [Fact]
    public void AnularUnaFacturaPagadaPasaAAnulada()
    {
        // Arrange
        var factura = FacturaPagada();

        // Act
        factura.Cancel();

        // Assert
        Assert.Equal(InvoiceState.Cancelled, factura.State);
    }

    [Fact]
    public void AnularUnaFacturaEnDeudaPasaAAnulada()
    {
        // Arrange
        var factura = FacturaEnDeuda();

        // Act
        factura.Cancel();

        // Assert
        Assert.Equal(InvoiceState.Cancelled, factura.State);
    }

    [Fact]
    public void AnularUnaFacturaEnVerificacionPendienteSeRechaza()
    {
        // Arrange
        var factura = FacturaEnVerificacionPendiente();

        // Act + Assert: el par no esta en la tabla y el estado no cambia.
        Assert.Throws<InvalidInvoiceTransitionException>(() => factura.Cancel());
        Assert.Equal(InvoiceState.PendingVerification, factura.State);
    }

    [Fact]
    public void AnularUnaFacturaNoEntregadaSeRechaza()
    {
        // Arrange: NotDelivered es terminal.
        var factura = FacturaNoEntregada();

        // Act + Assert
        Assert.Throws<InvalidInvoiceTransitionException>(() => factura.Cancel());
        Assert.Equal(InvoiceState.NotDelivered, factura.State);
    }

    [Fact]
    public void AnularUnaFacturaYaAnuladaSeRechaza()
    {
        // Arrange: Cancelled es terminal.
        var factura = FacturaEmitida();
        factura.Cancel();

        // Act + Assert
        Assert.Throws<InvalidInvoiceTransitionException>(() => factura.Cancel());
        Assert.Equal(InvoiceState.Cancelled, factura.State);
    }
}
