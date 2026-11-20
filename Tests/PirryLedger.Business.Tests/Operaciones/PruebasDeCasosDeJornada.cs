using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Tests.Operaciones;

public sealed class PruebasDeCasosDeJornada
{
    private static readonly DateTime Ahora = new(2026, 11, 19, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FacturaInconclusaDeCompaneroBloqueaCierreSinDarAccesoParaModificarla()
    {
        var e = new Escenario();
        var other = e.Invoice();
        var original = other.State;
        var error = await Assert.ThrowsAsync<UnfinishedInvoicesException>(() => e.Service.CloseAsync(e.Branch, e.Day.Id));
        Assert.Equal(other.Id, Assert.Single(error.Invoices).InvoiceId);
        Assert.Equal("Ana", error.Invoices[0].IssuerName);
        Assert.Equal(original, other.State);
        Assert.True(e.Day.IsOpen);
        Assert.Null(e.Day.Closing);
        Assert.Equal(0, e.Repository.Saves);
    }

    [Fact]
    public async Task ElCierreNoIncorporaAbonosNiAnulacionesPendientes()
    {
        var e = new Escenario();
        var paid = e.Invoice();
        paid.Collect([InvoicePayment.Cash(100m, null, Ahora, Guid.NewGuid())]);
        var debt = e.Invoice();
        debt.LeaveAsDebt("Cliente", [InvoicePayment.Cash(40m, null, Ahora, Guid.NewGuid())]);
        debt.RegisterDebtPayment(20m, Ahora, Guid.NewGuid());
        var pending = e.Invoice();
        pending.Collect([InvoicePayment.Cash(100m, null, Ahora, Guid.NewGuid())]);
        e.Pending.Ids.Add(pending.Id);

        await e.Service.CloseAsync(e.Branch, e.Day.Id);

        Assert.Equal(200m, e.Day.Closing!.Sold);
        Assert.Equal(140m, e.Day.Closing.Cash);
        Assert.Equal(60m, e.Day.Closing.Debt);
        Assert.Equal(100m, e.Day.Closing.PendingCancellation);
    }

    [Fact]
    public async Task ElCierreGuardadoNoCambiaCuandoDespuesSeAnulaUnaVenta()
    {
        var e = new Escenario();
        var invoice = e.Invoice();
        invoice.Collect([InvoicePayment.Cash(100m, null, Ahora, Guid.NewGuid())]);
        await e.Service.CloseAsync(e.Branch, e.Day.Id);
        invoice.Cancel();
        Assert.Equal(100m, e.Day.Closing!.Sold);
        Assert.Equal(100m, e.Day.Closing.Cash);
    }

    [Fact]
    public async Task UnaJornadaCerradaRequiereReaperturaExplicitaAntesDeEmitir()
    {
        var e = new Escenario();
        await e.Service.CloseAsync(e.Branch, e.Day.Id);
        var invoked = false;
        var error = await Assert.ThrowsAsync<BusinessDayRuleViolationException>(() =>
            e.Service.IssueAsync(e.Branch, _ => { invoked = true; return Task.FromResult(e.Invoice()); }, default));
        Assert.Equal("ReopenRequired", error.Code);
        Assert.False(invoked);
        Assert.False(e.Day.IsOpen);
    }

    [Fact]
    public async Task NoPuedeOperarseLaJornadaDeOtraSucursal()
    {
        var e = new Escenario();
        var error = await Assert.ThrowsAsync<BusinessDayRuleViolationException>(() => e.Service.CloseAsync(Guid.NewGuid(), e.Day.Id));
        Assert.Equal("BranchDenied", error.Code);
        Assert.True(e.Day.IsOpen);
        Assert.Equal(0, e.Repository.Saves);
    }

    private sealed class Escenario
    {
        public Guid Branch { get; } = Guid.NewGuid();
        public BusinessDay Day { get; }
        public Memoria Repository { get; } = new();
        public Pendientes Pending { get; } = new();
        public ManageBusinessDays Service { get; }
        public Escenario()
        {
            Day = BusinessDay.Open(Branch, Ahora);
            Repository.Days.Add(Day);
            Service = new ManageBusinessDays(Repository, new Alcance { BranchId = Branch, UserId = Guid.NewGuid() },
                Pending, new FakeClock(Ahora.AddHours(1)));
        }
        public Invoice Invoice()
        {
            var invoice = PirryLedger.Business.Domain.Entities.Invoice.Create();
            invoice.Issue([InvoiceItem.Create("Producto", 100m, 1)], true, Ahora, Branch, "Sucursal", "Dirección", Guid.NewGuid(), "Ana");
            invoice.AssignBusinessDay(Day.Id);
            Repository.Invoices.Add(invoice);
            return invoice;
        }
    }

    [Fact]
    public async Task JornadaVencidaAbiertaBloqueaSoloLaEmisionSinCrearOtra()
    {
        var e = new Escenario();
        var service = new ManageBusinessDays(e.Repository,
            new Alcance { BranchId = e.Branch, UserId = Guid.NewGuid() }, e.Pending,
            new FakeClock(e.Day.IssueDeadlineUtc));
        var invoked = false;
        var error = await Assert.ThrowsAsync<BusinessDayRuleViolationException>(() =>
            service.IssueAsync(e.Branch, _ => { invoked = true; return Task.FromResult(e.Invoice()); }, default));
        Assert.Equal("ClosingRequired", error.Code);
        Assert.False(invoked);
        Assert.Single(e.Repository.Days);
        await service.CloseAsync(e.Branch, e.Day.Id);
        Assert.False(e.Day.IsOpen);
    }

    [Fact]
    public async Task JornadaCerradaDespuesDelLimiteDaPasoAUnaNuevaConFechaActual()
    {
        var e = new Escenario();
        await e.Service.CloseAsync(e.Branch, e.Day.Id);
        var service = new ManageBusinessDays(e.Repository,
            new Alcance { BranchId = e.Branch, UserId = Guid.NewGuid() }, e.Pending,
            new FakeClock(e.Day.IssueDeadlineUtc));
        var invoice = await service.IssueAsync(e.Branch, id =>
        {
            var result = PirryLedger.Business.Domain.Entities.Invoice.Create();
            result.Issue([InvoiceItem.Create("Producto", 100m, 1)], true,
                e.Day.IssueDeadlineUtc, e.Branch, "Sucursal", "Dirección");
            result.AssignBusinessDay(id);
            return Task.FromResult(result);
        }, default);
        Assert.Equal(2, e.Repository.Days.Count);
        var opened = e.Repository.Days.Single(x => x.IsOpen);
        Assert.Equal(e.Day.BusinessDate.AddDays(1), opened.BusinessDate);
        Assert.Equal(opened.Id, invoice.BusinessDayId);
    }

    [Fact]
    public async Task OtroEstandarDeLaSucursalPuedeReabrirSinCambiarFechaNiCierreAnterior()
    {
        var e = new Escenario();
        await e.Service.CloseAsync(e.Branch, e.Day.Id);
        var original = e.Day.LastClosedByUserId;
        var service = new ManageBusinessDays(e.Repository,
            new Alcance { BranchId = e.Branch, UserId = Guid.NewGuid() }, e.Pending,
            new FakeClock(Ahora.AddHours(2)));
        await service.ReopenAsync(e.Branch, e.Day.Id);
        Assert.True(e.Day.IsReopened);
        Assert.Equal(original, e.Day.LastClosedByUserId);
        Assert.NotNull(e.Day.Closing);
    }

    internal sealed class Alcance : IBranchScopeAccessor
    {
        public Guid? BranchId { get; set; }
        public Guid? UserId { get; set; }
        public bool IsGlobal { get; set; }
    }
    internal sealed class Pendientes : IPendingInvoiceCancellations
    {
        public HashSet<Guid> Ids { get; } = new();
        public Task<IReadOnlySet<Guid>> FindAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlySet<Guid>>(Ids);
    }
    internal sealed class Memoria : IBusinessDayRepository
    {
        public List<BusinessDay> Days { get; } = new();
        public List<Invoice> Invoices { get; } = new();
        public int Saves { get; private set; }
        public Task<T> InBranchTransactionAsync<T>(Guid branch, Func<Task<T>> action, CancellationToken ct) => action();
        public Task<BusinessDay?> LatestAsync(Guid branch, CancellationToken ct) => Task.FromResult(Days.Where(x => x.BranchId == branch).OrderByDescending(x => x.OpenedAtUtc).FirstOrDefault());
        public Task<BusinessDay?> FindAsync(Guid id, Guid branch, CancellationToken ct) => Task.FromResult(Days.SingleOrDefault(x => x.Id == id && x.BranchId == branch));
        public Task<bool> HasLaterAsync(BusinessDay day, CancellationToken ct) => Task.FromResult(Days.Any(x => x.BranchId == day.BranchId && x.OpenedAtUtc > day.OpenedAtUtc));
        public Task AddAsync(BusinessDay day, CancellationToken ct) { Days.Add(day); return Task.CompletedTask; }
        public Task<IReadOnlyList<Invoice>> InvoicesAsync(Guid day, CancellationToken ct) => Task.FromResult<IReadOnlyList<Invoice>>(Invoices.Where(x => x.BusinessDayId == day).ToArray());
        public Task SaveAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }
    }
}
