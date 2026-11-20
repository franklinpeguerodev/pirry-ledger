using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Api.Models;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Infrastructure.Persistence;
using PirryLedger.Business.Tests.Imagenes;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDeRecuperacionPorHttp
{
    [Fact]
    public async Task ElEmisorRecuperaPorRutaExclusivaSinReabrirNiCambiarCierre()
    {
        await using var scenario = new Escenario();
        var invoice = scenario.Invoice;
        var client = scenario.Use(scenario.Owner);
        var body = new { payments = new[] { new { method = "Cash", amount = 500m } }, registeredByUserId = Guid.NewGuid() };

        var ordinary = await client.PostAsJsonAsync($"/api/billing/invoices/{invoice.Id}/collect", body);
        var recovered = await client.PostAsJsonAsync($"/api/billing/invoices/{invoice.Id}/recover", body);
        var response = (await recovered.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        var repeated = await client.PostAsJsonAsync($"/api/billing/invoices/{invoice.Id}/recover", body);
        await using var context = new BusinessDbContext(scenario.Options);
        var day = await context.BusinessDays.SingleAsync();
        var stored = await context.Invoices.Include(stored => stored.Payments).SingleAsync();

        Assert.Equal(HttpStatusCode.BadRequest, ordinary.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal("Paid", response.State);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        Assert.Equal(invoice.Id, response.Id);
        Assert.Equal(invoice.BusinessDayId, response.BusinessDayId);
        Assert.Equal(invoice.IssuedAtUtc, response.IssuedAtUtc);
        Assert.Equal(invoice.IssuedByUserId, Assert.Single(stored.Payments).RegisteredByUserId);
        Assert.Equal(scenario.Server.Clock.UtcNow, stored.Payments.Single().RegisteredAtUtc);
        Assert.False(day.IsOpen);
        Assert.Equal(0m, day.Closing!.Sold);
        Assert.Equal(500m, day.Closing.NotDelivered);
        Assert.Equal(1, await context.Invoices.CountAsync());
    }

    [Fact]
    public async Task TransferenciaRecuperadaEsVerificadaPorAdministrador()
    {
        await using var scenario = new Escenario();
        var client = scenario.Use(scenario.Owner);

        var recovered = await client.PostAsJsonAsync($"/api/billing/invoices/{scenario.Invoice.Id}/recover",
            new { payments = new[] { new { method = "Transfer", amount = 500m } } });
        var pending = (await recovered.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        scenario.Use(scenario.Admin);
        var confirmed = await client.PostAsync($"/api/billing/invoices/{scenario.Invoice.Id}/transfers/confirm", null);
        var paid = (await confirmed.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal("PendingVerification", pending.State);
        Assert.Equal("Registered", Assert.Single(pending.Payments).Status);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal("Paid", paid.State);
        Assert.Equal("Confirmed", Assert.Single(paid.Payments).Status);
        Assert.Equal(scenario.Invoice.BusinessDayId, paid.BusinessDayId);
    }

    [Fact]
    public async Task SinSesionAdministradorCompaneroYOtraSucursalNoRecuperanNiModificanLaFactura()
    {
        await using var scenario = new Escenario();
        var client = scenario.Server.Cliente;
        var path = $"/api/billing/invoices/{scenario.Invoice.Id}/recover";
        var body = new { payments = new[] { new { method = "Cash", amount = 500m } } };

        var anonymous = await client.PostAsJsonAsync(path, body);
        scenario.Use(scenario.Admin);
        var admin = await client.PostAsJsonAsync(path, body);
        scenario.Use(scenario.Peer);
        var peer = await client.PostAsJsonAsync(path, body);
        scenario.Use(scenario.Foreign);
        var foreign = await client.PostAsJsonAsync(path, body);
        await using var context = new BusinessDbContext(scenario.Options);
        var stored = await context.Invoices.Include(invoice => invoice.Payments).SingleAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, peer.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(InvoiceState.NotDelivered, stored.State);
        Assert.Empty(stored.Payments);
        Assert.Equal(scenario.Invoice.BusinessDayId, stored.BusinessDayId);
    }

    private sealed class Escenario : IAsyncDisposable
    {
        public DbContextOptions<BusinessDbContext> Options { get; } = new DbContextOptionsBuilder<BusinessDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public ServidorDePruebasHttp Server { get; }
        public string Owner { get; }
        public string Peer { get; }
        public string Foreign { get; }
        public string Admin { get; }
        public Invoice Invoice { get; }

        public Escenario()
        {
            Server = new ServidorDePruebasHttp(services =>
            {
                services.AddScoped(_ => new BusinessDbContext(Options));
                services.AddScoped<IInvoiceRepository, InvoiceRepository>();
                services.AddScoped<RecoverUndeliveredInvoice>();
                services.AddScoped<CollectInvoicePayment>();
                services.AddScoped<ConfirmInvoiceTransfer>();
            });
            var branch = Guid.NewGuid();
            Owner = Server.TokenDeStandard("ana@ejemplo.com", branch);
            Peer = Server.TokenDeStandard("juan@ejemplo.com", branch);
            Foreign = Server.TokenDeStandard("otra@ejemplo.com", Guid.NewGuid());
            Admin = Server.TokenDeAdministrator();
            var owner = Server.Usuarios.FindByEmailAsync("ana@ejemplo.com").GetAwaiter().GetResult()!;
            var issued = Server.Clock.UtcNow.AddDays(-1);
            var day = BusinessDay.Open(branch, issued);
            Invoice = PirryLedger.Business.Domain.Entities.Invoice.Create();
            Invoice.Issue([InvoiceItem.Create("Combo", 500m, 1)], true, issued, branch,
                "Sucursal", "Dirección", owner.Id, owner.Name);
            Invoice.AssignBusinessDay(day.Id);
            Invoice.MarkAsNotDelivered();
            day.Close(InvoiceSalesCalculation.Calculate([Invoice], new HashSet<Guid>()), issued.AddHours(1), owner.Id);
            using var context = new BusinessDbContext(Options);
            context.AddRange(day, Invoice);
            context.SaveChanges();
        }

        public HttpClient Use(string token)
        {
            Server.Cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return Server.Cliente;
        }

        public ValueTask DisposeAsync() => Server.DisposeAsync();
    }
}
