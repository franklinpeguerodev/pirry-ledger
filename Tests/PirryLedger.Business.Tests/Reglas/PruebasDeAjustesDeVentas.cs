using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Tests.Reglas;

public sealed class PruebasDeAjustesDeVentas
{
    private static readonly DateTime Ahora = new(2026, 11, 19, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SolicitudExcluyeVentaYEfectivoYRechazoLosReincorpora()
    {
        var invoice = Factura();
        invoice.Collect([InvoicePayment.Cash(500m, null, Ahora, Guid.NewGuid())]);
        var pending = new HashSet<Guid> { invoice.Id };

        var requested = InvoiceSalesCalculation.Calculate([invoice], pending);
        pending.Remove(invoice.Id);
        var rejected = InvoiceSalesCalculation.Calculate([invoice], pending);

        Assert.Equal(0m, requested.Sold);
        Assert.Equal(0m, requested.Cash);
        Assert.Equal(500m, requested.PendingCancellation);
        Assert.Equal(500m, rejected.Sold);
        Assert.Equal(500m, rejected.Cash);
        Assert.Equal(0m, rejected.PendingCancellation);
    }

    [Fact]
    public void AprobacionMantieneExclusionYSeparaImporteAnulado()
    {
        var invoice = Factura();
        invoice.Collect([InvoicePayment.Cash(500m, null, Ahora, Guid.NewGuid())]);
        var before = InvoiceSalesCalculation.Calculate([invoice], new HashSet<Guid> { invoice.Id });

        invoice.Cancel();
        var after = InvoiceSalesCalculation.Calculate([invoice], new HashSet<Guid>());

        Assert.Equal(0m, before.Sold);
        Assert.Equal(0m, after.Sold);
        Assert.Equal(0m, after.Cash);
        Assert.Equal(0m, after.PendingCancellation);
        Assert.Equal(500m, after.Cancelled);
    }

    [Fact]
    public void RecuperarActualizaCifrasSinModificarCierreGuardado()
    {
        var invoice = Factura();
        invoice.MarkAsNotDelivered();
        var day = BusinessDay.Open(invoice.BranchId, Ahora);
        invoice.AssignBusinessDay(day.Id);
        var original = InvoiceSalesCalculation.Calculate([invoice], new HashSet<Guid>());
        day.Close(original, Ahora.AddHours(1), Guid.NewGuid());

        invoice.RecoverUndelivered([InvoicePayment.Cash(500m, null, Ahora.AddDays(1), Guid.NewGuid())]);
        var updated = InvoiceSalesCalculation.Calculate([invoice], new HashSet<Guid>());

        Assert.Equal(0m, original.Sold);
        Assert.Equal(500m, original.NotDelivered);
        Assert.Equal(500m, updated.Sold);
        Assert.Equal(500m, updated.Cash);
        Assert.Equal(0m, updated.NotDelivered);
        Assert.Same(original, day.Closing);
        Assert.False(day.IsOpen);
        Assert.Equal(day.Id, invoice.BusinessDayId);
    }

    [Fact]
    public void AbonosNoSeSumanAEfectivoNiReducenFiadoOriginalDelCierre()
    {
        var invoice = Factura();
        invoice.LeaveAsDebt("Cliente", [InvoicePayment.Cash(100m, null, Ahora, Guid.NewGuid())]);
        invoice.RegisterDebtPayment(400m, Ahora.AddDays(1), Guid.NewGuid());

        var result = InvoiceSalesCalculation.Calculate([invoice], new HashSet<Guid>());

        Assert.Equal(500m, result.Sold);
        Assert.Equal(100m, result.Cash);
        Assert.Equal(400m, result.Debt);
    }

    [Fact]
    public void TransferenciaRecuperadaSeSeparaDeConfirmadasYNoEntregadas()
    {
        var invoice = Factura();
        invoice.MarkAsNotDelivered();
        invoice.RecoverUndelivered([InvoicePayment.Transfer(500m, Ahora, Guid.NewGuid())]);

        var pending = InvoiceSalesCalculation.Calculate([invoice], new HashSet<Guid>());
        invoice.ConfirmTransfer();
        var confirmed = InvoiceSalesCalculation.Calculate([invoice], new HashSet<Guid>());

        Assert.Equal(500m, pending.Sold);
        Assert.Equal(500m, pending.PendingTransfers);
        Assert.Equal(0m, pending.ConfirmedTransfers);
        Assert.Equal(0m, pending.NotDelivered);
        Assert.Equal(500m, confirmed.ConfirmedTransfers);
        Assert.Equal(0m, confirmed.PendingTransfers);
    }

    [Fact]
    public void UnaFacturaInconclusaNoEsUnaVenta()
    {
        var invoice = Factura();

        var result = InvoiceSalesCalculation.Calculate([invoice], new HashSet<Guid>());

        Assert.Equal(0m, result.Sold);
        Assert.Equal(0m, result.Cash);
    }

    private static Invoice Factura()
    {
        var invoice = Invoice.Create();
        invoice.Issue([InvoiceItem.Create("Combo", 500m, 1)], true, Ahora,
            Guid.NewGuid(), "Sucursal", "Dirección");
        return invoice;
    }
}
