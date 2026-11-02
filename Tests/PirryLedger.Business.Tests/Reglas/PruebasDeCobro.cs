using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Tests.Reglas;

// RF-NEG-02: cobrar es una funcionalidad separable. Efectivo exige el monto
// recibido y guarda el cambio; una transferencia manda la factura a
// verificacion pendiente. En delivery no se registra recibido ni cambio: no se
// sabe con exactitud cuanto devolvio el courier.
public sealed class PruebasDeCobro
{
    private static readonly DateTime Ahora = new(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc);

    private static readonly Guid Cajero = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Invoice FacturaEmitida(decimal total = 500m, bool isDelivery = false)
    {
        var factura = Invoice.Create();
        factura.Issue(new[] { InvoiceItem.Create("Combo", total, 1) }, isDelivery, Ahora);
        return factura;
    }

    [Fact]
    public void CobrarEnEfectivoQueCubreElTotalPasaAPagada()
    {
        // Arrange
        var factura = FacturaEmitida();

        // Act
        factura.Collect(new[] { InvoicePayment.Cash(500m, 500m, Ahora, Cajero) });

        // Assert
        Assert.Equal(InvoiceState.Paid, factura.State);
        Assert.Single(factura.Payments);
        Assert.Equal(500m, factura.PaidAmount);
    }

    [Fact]
    public void CobrarEnEfectivoGuardaElRecibidoYElCambio()
    {
        // Arrange
        var factura = FacturaEmitida();

        // Act
        factura.Collect(new[] { InvoicePayment.Cash(500m, 600m, Ahora, Cajero) });

        // Assert
        var pago = Assert.Single(factura.Payments);
        Assert.Equal(600m, pago.ReceivedAmount);
        Assert.Equal(100m, pago.ChangeAmount);
        Assert.Equal(PaymentStatus.Confirmed, pago.Status);
    }

    [Fact]
    public void CobrarConRecibidoMenorQueElMontoSeRechaza()
    {
        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(
            () => InvoicePayment.Cash(500m, 400m, Ahora, Cajero));
    }

    [Fact]
    public void CobrarConUnPagoQueExcedeElTotalSeRechaza()
    {
        // Arrange
        var factura = FacturaEmitida();
        var pagos = new[]
        {
            InvoicePayment.Cash(300m, 300m, Ahora, Cajero),
            InvoicePayment.Cash(300m, 300m, Ahora, Cajero),
        };

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(() => factura.Collect(pagos));
        Assert.Equal(InvoiceState.Issued, factura.State);
        Assert.Empty(factura.Payments);
    }

    [Fact]
    public void CobrarConTransferenciaPasaAVerificacionPendiente()
    {
        // Arrange
        var factura = FacturaEmitida();

        // Act
        factura.Collect(new[] { InvoicePayment.Transfer(500m, Ahora, Cajero) });

        // Assert
        Assert.Equal(InvoiceState.PendingVerification, factura.State);
        var pago = Assert.Single(factura.Payments);
        Assert.Equal(PaymentStatus.Registered, pago.Status);
    }

    [Fact]
    public void CobrarConEfectivoYTransferenciaPasaAVerificacionPendiente()
    {
        // Arrange
        var factura = FacturaEmitida();
        var pagos = new[]
        {
            InvoicePayment.Cash(200m, 200m, Ahora, Cajero),
            InvoicePayment.Transfer(300m, Ahora, Cajero),
        };

        // Act
        factura.Collect(pagos);

        // Assert
        Assert.Equal(InvoiceState.PendingVerification, factura.State);
        Assert.Equal(2, factura.Payments.Count);
    }

    [Fact]
    public void UnaFacturaDeDeliveryNoRegistraRecibidoNiCambio()
    {
        // Arrange
        var factura = FacturaEmitida(isDelivery: true);
        var conRecibido = new[] { InvoicePayment.Cash(500m, 600m, Ahora, Cajero) };

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(() => factura.Collect(conRecibido));
        Assert.Equal(InvoiceState.Issued, factura.State);
        Assert.Empty(factura.Payments);
    }

    [Fact]
    public void CobrarUnaFacturaDeDeliveryEnEfectivoGuardaSoloElMonto()
    {
        // Arrange
        var factura = FacturaEmitida(isDelivery: true);

        // Act
        factura.Collect(new[] { InvoicePayment.Cash(500m, null, Ahora, Cajero) });

        // Assert
        var pago = Assert.Single(factura.Payments);
        Assert.Null(pago.ReceivedAmount);
        Assert.Null(pago.ChangeAmount);
        Assert.Equal(InvoiceState.Paid, factura.State);
    }

    [Fact]
    public void UnAbonoDeDeudaNoSeRegistraComoCobroDeLaVenta()
    {
        // Arrange
        var factura = FacturaEmitida();
        var abono = InvoicePayment.Debt(500m, Ahora, Guid.Parse("99999999-9999-9999-9999-999999999999"));

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(() => factura.Collect(new[] { abono }));
        Assert.Equal(InvoiceState.Issued, factura.State);
        Assert.Empty(factura.Payments);
    }
}
