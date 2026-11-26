using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Infrastructure.Persistence;
using PirryLedger.Business.Tests.Imagenes;
using PirryLedger.Business.Tests.Operaciones;
using PirryLedger.Core.Contracts.Permissions;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDeDeudasPorHttp
{
    [Fact]
    public async Task UnCompaneroAbonaSuSucursalSinVerHistorialYElAdministradorConservaRechazados()
    {
        await using var scenario = new Escenario();
        var debt = scenario.Debt;
        var path = $"/api/billing/invoices/{debt.Id}/payments";
        var client = scenario.Use(scenario.Peer);

        var registered = await client.PostAsJsonAsync(path, new
        {
            amount = 500m,
            method = "Transfer",
            registeredByUserId = Guid.NewGuid(),
            registeredAtUtc = scenario.Server.Clock.UtcNow.AddYears(-5)
        });
        var pending = (await registered.Content.ReadFromJsonAsync<DebtSummary>())!;
        var blocked = await client.PostAsJsonAsync(path, new { amount = 100m, method = "Cash" });
        var hidden = await client.GetAsync($"/api/admin/debts/{debt.Id}/payments");
        var debtView = await client.GetFromJsonAsync<JsonElement>($"/api/billing/debts/{debt.Id}");

        scenario.Use(scenario.Admin);
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/admin/debts/{debt.Id}/payments");
        var paymentId = history.GetProperty("payments")[0].GetProperty("id").GetGuid();
        var peer = await scenario.Server.Usuarios.FindByEmailAsync("juan@ejemplo.com");
        var rejectPath = $"/api/admin/debts/{debt.Id}/payments/{paymentId}/reject";
        var noReason = await client.PostAsJsonAsync(rejectPath, new { reason = " " });
        var rejected = await client.PostAsJsonAsync(rejectPath, new { reason = "No llegó" });

        scenario.Use(scenario.Owner);
        var ordinary = await client.GetFromJsonAsync<JsonElement>($"/api/billing/invoices/{debt.Id}");
        scenario.Use(scenario.Peer);
        var cash = await client.PostAsJsonAsync(path, new { amount = 1000m, method = "Cash" });
        var settled = (await cash.Content.ReadFromJsonAsync<DebtSummary>())!;
        var ownDebts = await client.GetFromJsonAsync<DebtSummary[]>("/api/billing/debts");
        scenario.Use(scenario.Admin);
        var finalHistory = await client.GetFromJsonAsync<JsonElement>($"/api/admin/debts/{debt.Id}/payments");

        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        Assert.Equal(peer!.Id, history.GetProperty("payments")[0].GetProperty("registeredByUserId").GetGuid());
        Assert.Equal(scenario.Server.Clock.UtcNow, history.GetProperty("payments")[0].GetProperty("registeredAtUtc").GetDateTime());
        Assert.Equal(1000m, pending.OutstandingAmount);
        Assert.True(pending.HasPendingTransfer);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
        Assert.False(debtView.TryGetProperty("payments", out _));
        Assert.False(debtView.TryGetProperty("paidAtUtc", out _));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Empty(ordinary.GetProperty("payments").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, cash.StatusCode);
        Assert.Equal(0m, settled.OutstandingAmount);
        Assert.True(settled.IsSettled);
        Assert.Contains(ownDebts!, summary => summary.InvoiceId == debt.Id && summary.IsSettled);
        var payments = finalHistory.GetProperty("payments").EnumerateArray().ToArray();
        Assert.Equal(2, payments.Length);
        Assert.Contains(payments, payment => payment.GetProperty("status").GetString() == "Rejected"
            && payment.GetProperty("rejectionReason").GetString() == "No llegó");
    }

    [Fact]
    public async Task AdministradorPuedeRegistrarYConfirmarSuPropiaTransferenciaConFechaOriginal()
    {
        await using var scenario = new Escenario();
        var client = scenario.Use(scenario.Admin);
        var registeredAt = scenario.Server.Clock.UtcNow;
        var registered = await client.PostAsJsonAsync($"/api/billing/invoices/{scenario.Debt.Id}/payments",
            new { amount = 1000m, method = "Transfer" });
        var before = await client.GetFromJsonAsync<JsonElement>($"/api/admin/debts/{scenario.Debt.Id}/payments");
        var payment = before.GetProperty("payments")[0];
        var paymentId = payment.GetProperty("id").GetGuid();
        scenario.Server.Clock.Advance(TimeSpan.FromHours(1));

        var confirmed = await client.PostAsync($"/api/admin/debts/{scenario.Debt.Id}/payments/{paymentId}/confirm", null);
        var repeated = await client.PostAsync($"/api/admin/debts/{scenario.Debt.Id}/payments/{paymentId}/confirm", null);
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/admin/debts/{scenario.Debt.Id}/payments");
        var after = history.GetProperty("payments")[0];

        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        Assert.Equal(registeredAt, history.GetProperty("paidAtUtc").GetDateTime());
        Assert.Equal(after.GetProperty("registeredByUserId").GetGuid(), after.GetProperty("resolvedByUserId").GetGuid());
        Assert.Equal("Confirmed", after.GetProperty("status").GetString());
        Assert.Single(history.GetProperty("payments").EnumerateArray());
    }

    [Fact]
    public async Task NoSePuedeAbonarOtraSucursalNiUnaFacturaConAnulacionPendiente()
    {
        await using var scenario = new Escenario();
        var client = scenario.Server.Cliente;
        var noSession = await client.GetAsync("/api/billing/debts");
        scenario.Use(scenario.Peer);
        var other = await client.PostAsJsonAsync($"/api/billing/invoices/{scenario.ForeignDebt.Id}/payments",
            new { amount = 100m, method = "Cash" });
        var foreignRead = await client.GetAsync($"/api/billing/debts/{scenario.ForeignDebt.Id}");
        var foreignFilter = await client.GetAsync($"/api/billing/debts?branchId={scenario.ForeignDebt.BranchId}");
        scenario.Pending.Ids.Add(scenario.Debt.Id);
        var cancelled = await client.PostAsJsonAsync($"/api/billing/invoices/{scenario.Debt.Id}/payments",
            new { amount = 100m, method = "Cash" });
        scenario.Use(scenario.Admin);
        var current = await client.GetFromJsonAsync<JsonElement>($"/api/admin/debts/{scenario.Debt.Id}/payments");
        var foreignHistory = await client.GetFromJsonAsync<JsonElement>($"/api/admin/debts/{scenario.ForeignDebt.Id}/payments");

        Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreignFilter.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cancelled.StatusCode);
        Assert.Empty(current.GetProperty("payments").EnumerateArray());
        Assert.Empty(foreignHistory.GetProperty("payments").EnumerateArray());
    }

    [Fact]
    public async Task TransferenciaPendienteSoloBloqueaSuFacturaYNoRequiereCajaAbierta()
    {
        await using var scenario = new Escenario();
        var client = scenario.Use(scenario.Peer);
        var transfer = await client.PostAsJsonAsync($"/api/billing/invoices/{scenario.Debt.Id}/payments",
            new { amount = 500m, method = "Transfer" });
        var cash = await client.PostAsJsonAsync($"/api/billing/invoices/{scenario.SecondDebt.Id}/payments",
            new { amount = 100m, method = "Cash" });
        var second = (await cash.Content.ReadFromJsonAsync<DebtSummary>())!;
        await using var context = new BusinessDbContext(scenario.Options);

        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cash.StatusCode);
        Assert.Equal(900m, second.OutstandingAmount);
        Assert.Empty(await context.BusinessDays.ToListAsync());
        Assert.Equal(3, await context.Invoices.CountAsync());
    }

    [Fact]
    public async Task UnaFacturaFiadaConPagoParcialTampocoExponeHistorialAlEstandar()
    {
        await using var scenario = new Escenario();
        var invoice = Invoice.Create();
        invoice.Issue([InvoiceItem.Create("Producto", 1000m, 1)], true, scenario.Server.Clock.UtcNow,
            scenario.Debt.BranchId, "Sucursal", "Dirección", scenario.Debt.IssuedByUserId, "Ana");
        invoice.LeaveAsDebt("Cliente", [InvoicePayment.Cash(200m, null, scenario.Server.Clock.UtcNow, invoice.IssuedByUserId)]);
        await using (var context = new BusinessDbContext(scenario.Options))
        {
            context.Add(invoice);
            await context.SaveChangesAsync();
        }

        var client = scenario.Use(scenario.Owner);
        var standard = await client.GetFromJsonAsync<JsonElement>($"/api/billing/invoices/{invoice.Id}");
        scenario.Use(scenario.Admin);
        var admin = await client.GetFromJsonAsync<JsonElement>($"/api/admin/debts/{invoice.Id}/payments");

        Assert.Empty(standard.GetProperty("payments").EnumerateArray());
        Assert.Equal(800m, standard.GetProperty("balance").GetDecimal());
        Assert.Single(admin.GetProperty("payments").EnumerateArray());
    }

    private sealed class Escenario : IAsyncDisposable
    {
        public DbContextOptions<BusinessDbContext> Options { get; } = new DbContextOptionsBuilder<BusinessDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public ServidorDePruebasHttp Server { get; }
        public string Owner { get; }
        public string Peer { get; }
        public string Admin { get; }
        public Invoice Debt { get; }
        public Invoice SecondDebt { get; }
        public Invoice ForeignDebt { get; }
        public PruebasDeCasosDeJornada.Pendientes Pending { get; } = new();

        public Escenario()
        {
            Server = new ServidorDePruebasHttp(services =>
            {
                services.AddScoped(_ => new BusinessDbContext(Options));
                services.AddScoped<IInvoiceRepository, InvoiceRepository>();
                services.AddScoped<IDebtRepository, SinBloqueoDePostgres>();
                services.AddSingleton<IPendingInvoiceCancellations>(Pending);
                services.AddScoped<ManageDebtPayments>();
                services.AddScoped<RegisterDebtPayment>();
                services.AddScoped<GetInvoice>();
            });
            var branch = Guid.NewGuid();
            Owner = Server.TokenDeStandard("ana@ejemplo.com", branch);
            Peer = Server.TokenDeStandard("juan@ejemplo.com", branch);
            Admin = Server.TokenDeAdministrator();
            var user = Server.Usuarios.FindByEmailAsync("ana@ejemplo.com").GetAwaiter().GetResult()!;
            Debt = Create(branch, user.Id);
            SecondDebt = Create(branch, user.Id);
            ForeignDebt = Create(Guid.NewGuid(), Guid.NewGuid());
            using var context = new BusinessDbContext(Options);
            context.AddRange(Debt, SecondDebt, ForeignDebt);
            context.SaveChanges();
        }

        private Invoice Create(Guid branch, Guid issuer)
        {
            var invoice = Invoice.Create();
            invoice.Issue([InvoiceItem.Create("Producto", 1000m, 1)], true,
                Server.Clock.UtcNow.AddYears(-1), branch, "Sucursal", "Dirección", issuer, "Ana");
            invoice.LeaveAsDebt("Cliente", null);
            return invoice;
        }

        public HttpClient Use(string token)
        {
            Server.Cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return Server.Cliente;
        }

        public ValueTask DisposeAsync() => Server.DisposeAsync();
    }

    // Consultas reales EF; solo el bloqueo físico se sustituye en estas pruebas sin base.
    private sealed class SinBloqueoDePostgres : IDebtRepository
    {
        private readonly DebtRepository _repository;

        public SinBloqueoDePostgres(BusinessDbContext context)
        {
            _repository = new DebtRepository(context);
        }

        public Task<IReadOnlyList<Invoice>> ListAsync(Guid? branch, CancellationToken cancellationToken) =>
            _repository.ListAsync(branch, cancellationToken);
        public Task<Invoice?> FindAsync(Guid invoice, Guid? branch, CancellationToken cancellationToken) =>
            _repository.FindAsync(invoice, branch, cancellationToken);
        public Task SaveAsync(CancellationToken cancellationToken) => _repository.SaveAsync(cancellationToken);

        public async Task<T> InInvoiceTransactionAsync<T>(Guid invoice, Guid? branch,
            Func<Invoice, Task<T>> action, CancellationToken cancellationToken) =>
            await action(await FindAsync(invoice, branch, cancellationToken) ?? throw new InvoiceNotFoundException());
    }
}
