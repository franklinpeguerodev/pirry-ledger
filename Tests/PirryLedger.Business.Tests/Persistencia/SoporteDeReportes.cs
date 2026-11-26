using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Infrastructure.Persistence;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Reports;

namespace PirryLedger.Business.Tests.Persistencia;

internal sealed class SoporteDeReportes : IAsyncDisposable
{
    public static readonly DateTime Ahora = new(2026, 11, 25, 22, 0, 0, DateTimeKind.Utc);
    public Guid A { get; } = Guid.NewGuid();
    public Guid B { get; } = Guid.NewGuid();
    public Guid Ana { get; } = Guid.NewGuid();
    public Guid Luis { get; } = Guid.NewGuid();
    public BusinessDbContext Context { get; }
    public Pendientes Cancellations { get; } = new();
    public BusinessReportRepository Repository { get; }
    public BillingReportSource Source { get; }
    public Invoice Combo { get; private set; } = null!;
    public Invoice Debt { get; private set; } = null!;
    public Invoice Settled { get; private set; } = null!;
    public Invoice PendingCancellation { get; private set; } = null!;
    public Invoice Undelivered { get; private set; } = null!;
    public BusinessDay ClosedA { get; private set; } = null!;
    public BusinessDay Reopened { get; private set; } = null!;
    public BusinessReportFilter Filter => new(new(2026, 11, 24), new(2026, 11, 25), [A, B], []);

    private SoporteDeReportes()
    {
        Context = new(new DbContextOptionsBuilder<BusinessDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Repository = new(Context);
        Source = new(Repository, Cancellations);
    }

    public static async Task<SoporteDeReportes> CrearAsync()
    {
        var fixture = new SoporteDeReportes();
        await fixture.PrepararAsync();
        return fixture;
    }

    private async Task PrepararAsync()
    {
        ClosedA = BusinessDay.Open(A, Ahora.AddDays(-1));
        var closedB = BusinessDay.Open(B, Ahora.AddDays(-1));
        Combo = Nueva(ClosedA, "Ana nueva", [InvoiceItem.Create("A", 100m, 1), InvoiceItem.Create("B", 50m, 1)], true);
        Cobrar(Combo);
        Debt = Nueva(ClosedA, "Ana", [InvoiceItem.Create("A", 120m, 1)]);
        Debt.LeaveAsDebt("Cliente pendiente", null);
        var transfer = Nueva(ClosedA, "Ana", [InvoiceItem.Create("Transferencia", 100m, 1)]);
        transfer.Collect([InvoicePayment.Transfer(100m, transfer.IssuedAtUtc.AddMinutes(1), Ana)]);
        PendingCancellation = Nueva(ClosedA, "Ana", [InvoiceItem.Create("Pendiente", 60m, 1)]);
        Cobrar(PendingCancellation);
        Cancellations.Ids.Add(PendingCancellation.Id);
        var cancelled = Nueva(ClosedA, "Ana", [InvoiceItem.Create("Anulada", 70m, 1)]);
        Cobrar(cancelled);
        cancelled.Cancel();
        Undelivered = Nueva(ClosedA, "Ana", [InvoiceItem.Create("No entregada", 50m, 1)]);
        Undelivered.MarkAsNotDelivered();
        Settled = Nueva(ClosedA, "Ana", [InvoiceItem.Create("A", 40m, 1)]);
        Settled.LeaveAsDebt("Cliente saldado", null);
        var second = Nueva(closedB, "Luis", [InvoiceItem.Create("Otro", 150m, 1)]);
        Cobrar(second);
        var firstRows = new[] { Combo, Debt, transfer, PendingCancellation, cancelled, Undelivered, Settled };
        ClosedA.Close(InvoiceSalesCalculation.Calculate(firstRows, Cancellations.Ids), Ahora.AddHours(-14), Ana);
        closedB.Close(InvoiceSalesCalculation.Calculate([second], Cancellations.Ids), Ahora.AddHours(-14), Luis);
        Debt.RegisterDebtPayment(20m, Ahora, Ana);
        Debt.RegisterDebtPayment(30m, Ahora.AddMinutes(1), Ana, PaymentMethod.Transfer);
        Settled.RegisterDebtPayment(40m, Ahora, Ana, PaymentMethod.Transfer);
        Settled.ConfirmDebtTransfer(Settled.Payments.Single().Id, Ahora.AddDays(1), Luis);
        var open = BusinessDay.Open(A, Ahora.AddHours(-2));
        var openSale = Nueva(open, "Ana", [InvoiceItem.Create("Abierta", 80m, 1)]);
        Cobrar(openSale);
        Reopened = BusinessDay.Open(B, Ahora.AddHours(-2));
        var before = Nueva(Reopened, "Luis", [InvoiceItem.Create("Antes", 50m, 1)]);
        Cobrar(before);
        Reopened.Close(InvoiceSalesCalculation.Calculate([before], Cancellations.Ids), Ahora.AddHours(-1), Luis);
        Reopened.Reopen(Ahora, false);
        var after = Nueva(Reopened, "Luis", [InvoiceItem.Create("Después", 20m, 1)], issuedAt: Ahora.AddMinutes(1));
        Cobrar(after);
        Context.AddRange(ClosedA, closedB, open, Reopened);
        Context.AddRange(firstRows);
        Context.AddRange(second, openSale, before, after);
        await Context.SaveChangesAsync();
    }

    private Invoice Nueva(BusinessDay day, string name, InvoiceItem[] items, bool afterMidnight = false, DateTime? issuedAt = null)
    {
        var invoice = Invoice.Create();
        invoice.Issue(items, true, issuedAt ?? (afterMidnight ? day.OpenedAtUtc.AddHours(8) : day.OpenedAtUtc),
            day.BranchId, day.BranchId == A ? "Sucursal A" : "Sucursal B", "Dirección",
            day.BranchId == A ? Ana : Luis, name);
        invoice.AssignBusinessDay(day.Id);
        return invoice;
    }
    private static void Cobrar(Invoice invoice) =>
        invoice.Collect([InvoicePayment.Cash(invoice.Total, null, invoice.IssuedAtUtc.AddMinutes(1), invoice.IssuedByUserId)]);
    public ValueTask DisposeAsync() => Context.DisposeAsync();

    internal sealed class Pendientes : IPendingInvoiceCancellations
    {
        public HashSet<Guid> Ids { get; } = [];
        public Task<IReadOnlySet<Guid>> FindAsync(IReadOnlyCollection<Guid> invoiceIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<Guid>>(Ids.Intersect(invoiceIds).ToHashSet());
    }
}
