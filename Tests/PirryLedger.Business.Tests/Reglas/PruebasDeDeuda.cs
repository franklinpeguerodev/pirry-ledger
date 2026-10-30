using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Tests.Reglas;

// RF-NEG-02: deuda y abonos. Dejar como deuda exige el nombre de la persona y
// admite un pago parcial. Un abono que cubre lo que falta pasa la factura a
// pagada en la misma operacion; un abono mayor que la deuda se rechaza.
public sealed class PruebasDeDeuda
{
    private static readonly DateTime Ahora = new(2026, 10, 29, 16, 0, 0, DateTimeKind.Utc);

    private static readonly Guid Cajero = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid Admin = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private static Invoice FacturaEmitida(decimal total = 500m)
    {
        var factura = Invoice.Create();
        factura.Issue(new[] { InvoiceItem.Create("Combo", total, 1) }, isDelivery: false, Ahora);
        return factura;
    }

    [Fact]
    public void DejarComoDeudaGuardaElNombreYElPagoParcial()
    {
        // Arrange
        var factura = FacturaEmitida();

        // Act
        factura.LeaveAsDebt("Juan", new[] { InvoicePayment.Cash(200m, 200m, Ahora, Cajero) }, Ahora, Cajero);

        // Assert
        Assert.Equal(InvoiceState.InDebt, factura.State);
        Assert.Equal("Juan", factura.DebtorName);
        Assert.Equal(200m, factura.PaidAmount);
    }

    [Fact]
    public void DejarComoDeudaSinNombreSeRechaza()
    {
        // Arrange
        var factura = FacturaEmitida();

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(
            () => factura.LeaveAsDebt("   ", null, Ahora, Cajero));
        Assert.Equal(InvoiceState.Issued, factura.State);
    }

    [Fact]
    public void DejarComoDeudaSinPagosQuedaEnDeuda()
    {
        // Arrange
        var factura = FacturaEmitida();

        // Act
        factura.LeaveAsDebt("Juan", null, Ahora, Cajero);

        // Assert
        Assert.Equal(InvoiceState.InDebt, factura.State);
        Assert.Equal(0m, factura.PaidAmount);
    }

    [Fact]
    public void AbonarParcialNoCambiaElEstadoYSumaAlPagado()
    {
        // Arrange
        var factura = FacturaEmitida();
        factura.LeaveAsDebt("Juan", null, Ahora, Cajero);

        // Act
        factura.RegisterDebtPayment(100m, Ahora, Admin);

        // Assert
        Assert.Equal(InvoiceState.InDebt, factura.State);
        Assert.Equal(100m, factura.PaidAmount);
    }

    [Fact]
    public void ElAbonoQueCubreElTotalPasaAPagada()
    {
        // Arrange
        var factura = FacturaEmitida();
        factura.LeaveAsDebt("Juan", null, Ahora, Cajero);

        // Act
        factura.RegisterDebtPayment(500m, Ahora, Admin);

        // Assert
        Assert.Equal(InvoiceState.Paid, factura.State);
        Assert.Equal(500m, factura.PaidAmount);
    }

    [Fact]
    public void UnAbonoMayorQueLoQueSeDebeSeRechaza()
    {
        // Arrange
        var factura = FacturaEmitida();
        factura.LeaveAsDebt("Juan", new[] { InvoicePayment.Cash(200m, 200m, Ahora, Cajero) }, Ahora, Cajero);

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(
            () => factura.RegisterDebtPayment(400m, Ahora, Admin));
        Assert.Equal(InvoiceState.InDebt, factura.State);
        Assert.Equal(200m, factura.PaidAmount);
    }

    [Fact]
    public void AbonarUnaFacturaQueNoEstaEnDeudaSeRechaza()
    {
        // Arrange
        var factura = FacturaEmitida();

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(
            () => factura.RegisterDebtPayment(100m, Ahora, Admin));
        Assert.Equal(InvoiceState.Issued, factura.State);
        Assert.Empty(factura.Payments);
    }

    [Fact]
    public void AbonarUnaFacturaPagadaSeRechazaYNoRegistraElPago()
    {
        // Arrange
        var factura = FacturaEmitida();
        factura.Collect(new[] { InvoicePayment.Cash(500m, 500m, Ahora, Cajero) });
        var pagosAntes = factura.Payments.Count;

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(
            () => factura.RegisterDebtPayment(100m, Ahora, Admin));
        Assert.Equal(InvoiceState.Paid, factura.State);
        Assert.Equal(pagosAntes, factura.Payments.Count);
    }

    [Fact]
    public void ConfirmarLaTransferenciaPasaAPagadaYConfirmaElPago()
    {
        // Arrange
        var factura = FacturaEmitida();
        factura.Collect(new[] { InvoicePayment.Transfer(500m, Ahora, Cajero) });
        Assert.Equal(InvoiceState.PendingVerification, factura.State);

        // Act
        factura.ConfirmTransfer(Ahora);

        // Assert
        Assert.Equal(InvoiceState.Paid, factura.State);
        Assert.All(factura.Payments, p => Assert.Equal(PaymentStatus.Confirmed, p.Status));
    }

    [Fact]
    public void ConfirmarUnaTransferenciaQueNoCubreElTotalSeRechaza()
    {
        // Arrange
        var factura = FacturaEmitida();
        // Partial transfer: payment 200 of 500 total → PendingVerification (HasTransfer)
        factura.Collect(new[] { InvoicePayment.Transfer(200m, Ahora, Cajero) });

        // Act + Assert
        Assert.Throws<InvoiceConditionNotMetException>(() => factura.ConfirmTransfer(Ahora));
        Assert.Equal(InvoiceState.PendingVerification, factura.State);
    }

    [Fact]
    public void ConfirmarUnaTransferenciaFueraDeVerificacionPendienteSeRechaza()
    {
        // Arrange
        var factura = FacturaEmitida();
        factura.Collect(new[] { InvoicePayment.Cash(500m, 500m, Ahora, Cajero) });

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(() => factura.ConfirmTransfer(Ahora));
        Assert.Equal(InvoiceState.Paid, factura.State);
    }

    [Fact]
    public void RechazarLaTransferenciaDejaElPagoRechazadoYPasaADeuda()
    {
        // Arrange
        var factura = FacturaEmitida();
        factura.Collect(new[] { InvoicePayment.Transfer(500m, Ahora, Cajero) });

        // Act
        factura.RejectTransfer("Juan", Ahora);

        // Assert
        Assert.Equal(InvoiceState.InDebt, factura.State);
        Assert.All(factura.Payments, p => Assert.Equal(PaymentStatus.Rejected, p.Status));
        Assert.Equal(0m, factura.PaidAmount);
    }

    [Fact]
    public void RechazarUnaTransferenciaFueraDeVerificacionPendienteSeRechaza()
    {
        // Arrange
        var factura = FacturaEmitida();

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(() => factura.RejectTransfer("Juan", Ahora));
        Assert.Equal(InvoiceState.Issued, factura.State);
        Assert.Empty(factura.Payments);
        Assert.Null(factura.DebtorName);
    }
}
