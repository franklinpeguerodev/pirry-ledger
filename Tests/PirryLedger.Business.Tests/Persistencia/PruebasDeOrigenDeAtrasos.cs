using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Infrastructure.Persistence;
using PirryLedger.Core.Contracts.Notifications;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDeOrigenDeAtrasos
{
    private static readonly DateTime Ahora = new(2026, 11, 25, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SeleccionaVentaAbonoYJornadaVencidosSinConfundirFechaDeFacturaConRegistro()
    {
        await using var context = CrearContexto();
        var day = BusinessDay.Open(Guid.NewGuid(), Ahora.AddDays(-6));
        var sale = Factura(day);
        sale.Collect([InvoicePayment.Transfer(100m, Ahora.AddDays(-5), Guid.NewGuid())]);
        var debt = Factura(day);
        debt.LeaveAsDebt("Cliente", null);
        debt.RegisterDebtPayment(50m, Ahora.AddDays(-5), Guid.NewGuid(), PaymentMethod.Transfer);
        var recent = Factura(day);
        recent.Collect([InvoicePayment.Transfer(100m, Ahora.AddDays(-4), Guid.NewGuid())]);
        context.AddRange(day, sale, debt, recent);
        await context.SaveChangesAsync();
        var source = new BillingOverdueNotificationSource(new OverdueBillingRepository(context));

        var candidates = await source.FindCandidatesAsync(Ahora);

        Assert.Equal(3, candidates.Count);
        Assert.Contains(candidates, item => item.SourceId == sale.Payments.Single().Id);
        Assert.Contains(candidates, item => item.SourceId == debt.Payments.Single().Id);
        Assert.Contains(candidates, item => item.Kind == NotificationEventKind.BusinessDayOverdue && item.SourceId == day.Id);
        Assert.DoesNotContain(candidates, item => item.SourceId == recent.Payments.Single().Id);
        Assert.Equal(InvoiceState.InDebt, debt.State);
        Assert.True(day.IsOpen);
    }

    [Fact]
    public async Task EstadoActualExcluyeTransferenciaConfirmadaFacturaAnuladaYJornadaCerrada()
    {
        await using var context = CrearContexto();
        var day = BusinessDay.Open(Guid.NewGuid(), Ahora.AddDays(-6));
        var confirmed = Factura(day);
        confirmed.Collect([InvoicePayment.Transfer(100m, Ahora.AddDays(-5), Guid.NewGuid())]);
        confirmed.ConfirmTransfer(Ahora, Guid.NewGuid());
        var cancelled = Factura(day);
        cancelled.LeaveAsDebt("Cliente", [InvoicePayment.Transfer(50m, Ahora.AddDays(-5), Guid.NewGuid())]);
        cancelled.Cancel();
        day.Close(InvoiceSalesCalculation.Calculate([confirmed, cancelled], new HashSet<Guid>()), Ahora, Guid.NewGuid());
        context.AddRange(day, confirmed, cancelled);
        await context.SaveChangesAsync();
        var source = new BillingOverdueNotificationSource(new OverdueBillingRepository(context));

        var candidates = await source.FindCandidatesAsync(Ahora);

        Assert.Empty(candidates);
        Assert.False(await source.IsRelevantAsync(NotificationEventKind.TransferOverdue, confirmed.Payments.Single().Id, Ahora));
        Assert.False(await source.IsRelevantAsync(NotificationEventKind.TransferOverdue, cancelled.Payments.Single().Id, Ahora));
        Assert.False(await source.IsRelevantAsync(NotificationEventKind.BusinessDayOverdue, day.Id, Ahora));
        Assert.Equal(PaymentStatus.Registered, cancelled.Payments.Single().Status);
    }

    [Fact]
    public async Task RevalidacionDeGeneracionExigeTransaccionSinModificarFactura()
    {
        await using var context = CrearContexto();
        var source = new BillingOverdueNotificationSource(new OverdueBillingRepository(context));
        var candidate = new OverdueNotificationCandidate(NotificationEventKind.TransferOverdue, Guid.NewGuid(), Ahora);

        await Assert.ThrowsAsync<InvalidOperationException>(() => source.RevalidateAsync(candidate, Ahora));

        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static BusinessDbContext CrearContexto() => new(new DbContextOptionsBuilder<BusinessDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Invoice Factura(BusinessDay day)
    {
        var invoice = Invoice.Create();
        invoice.Issue([InvoiceItem.Create("Combo", 100m, 1)], true, day.OpenedAtUtc,
            day.BranchId, "Sucursal", "Dirección", Guid.NewGuid(), "Cajero");
        invoice.AssignBusinessDay(day.Id);
        return invoice;
    }
}
