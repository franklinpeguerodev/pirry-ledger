using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Tests.Reglas;

// RF-NEG-02: marcar no entregada. Solo una factura de delivery puede quedar en
// NotDelivered, y solo cuando esta emitida.
public sealed class PruebasDeNoEntrega
{
    private static readonly DateTime Ahora = new(2026, 10, 29, 17, 0, 0, DateTimeKind.Utc);

    private static Invoice FacturaEmitida(bool isDelivery)
    {
        var factura = Invoice.Create();
        factura.Issue(new[] { InvoiceItem.Create("Combo", 500m, 1) }, isDelivery, Ahora);
        return factura;
    }

    [Fact]
    public void MarcarNoEntregadaUnaFacturaDeDeliveryPasaANoEntregada()
    {
        // Arrange
        var factura = FacturaEmitida(isDelivery: true);

        // Act
        factura.MarkAsNotDelivered();

        // Assert
        Assert.Equal(InvoiceState.NotDelivered, factura.State);
    }

    [Fact]
    public void MarcarNoEntregadaUnaFacturaQueNoEsDeliverySeRechaza()
    {
        // Arrange
        var factura = FacturaEmitida(isDelivery: false);

        // Act + Assert
        Assert.Throws<InvoiceConditionNotMetException>(() => factura.MarkAsNotDelivered());
        Assert.Equal(InvoiceState.Issued, factura.State);
    }

    [Fact]
    public void UnaFacturaEnVerificacionPendienteNoSeEdita()
    {
        // Arrange
        var factura = FacturaEmitida(isDelivery: false);
        factura.Collect(new[] { InvoicePayment.Transfer(500m, Ahora, Guid.Parse("11111111-1111-1111-1111-111111111111")) });

        // Act + Assert
        Assert.Throws<InvalidInvoiceTransitionException>(() => factura.MarkAsNotDelivered());
        Assert.Equal(InvoiceState.PendingVerification, factura.State);
    }
}
