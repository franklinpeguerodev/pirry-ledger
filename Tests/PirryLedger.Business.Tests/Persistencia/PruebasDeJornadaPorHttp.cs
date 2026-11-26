using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Api.Models;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Infrastructure.Persistence;
using PirryLedger.Business.Tests.Imagenes;
using PirryLedger.Business.Tests.Operaciones;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Permissions;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDeJornadaPorHttp
{
    [Fact]
    public async Task AbrirCerrarYReabrirRespetaSesionSucursalYPendientesDeTodosLosEmisores()
    {
        // Arrange
        var branch = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<BusinessDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var days = new PruebasDeCasosDeJornada.Memoria();
        await using var server = new ServidorDePruebasHttp(services =>
        {
            services.AddScoped(_ => new BusinessDbContext(options));
            services.AddScoped<IInvoiceRepository, InvoiceRepository>();
            services.AddSingleton<IBusinessDayRepository>(days);
            services.AddSingleton<IPendingInvoiceCancellations>(new PruebasDeCasosDeJornada.Pendientes());
            services.AddScoped<ManageBusinessDays>();
            services.AddScoped<IssueInvoice>();
            services.AddScoped<CollectInvoicePayment>();
            services.AddSingleton<IBranchDirectory>(new Directorio(branch));
        });
        var owner = server.TokenDeStandard("ana@ejemplo.com", branch);
        var peer = server.TokenDeStandard("juan@ejemplo.com", branch);
        var outsider = server.TokenDeStandard("otro@ejemplo.com");
        var product = server.CrearProducto();
        var client = server.Cliente;
        var unauthenticated = await client.GetAsync($"/api/billing/branches/{branch}/business-days/current");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner);

        // Act
        var issued = await client.PostAsJsonAsync("/api/billing/invoices", new
        { isDelivery = true, items = new[] { new { productId = product.Id, quantity = 1 } } });
        var invoice = (await issued.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        var day = Assert.Single(days.Days);
        await using (var context = new BusinessDbContext(options))
            days.Invoices.Add(await context.Invoices.Include(x => x.Items).Include(x => x.Payments).SingleAsync());
        var route = $"/api/billing/branches/{branch}/business-days/{day.Id}";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsider);
        var denied = await client.PostAsync(route + "/close", null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", peer);
        var blocked = await client.PostAsync(route + "/close", null);
        var detail = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner);
        var collected = await client.PostAsJsonAsync($"/api/billing/invoices/{invoice.Id}/collect", new
        { payments = new[] { new { method = "Cash", amount = 500m } } });
        await using (var context = new BusinessDbContext(options))
        {
            days.Invoices.Clear();
            days.Invoices.Add(await context.Invoices.Include(x => x.Items).Include(x => x.Payments).SingleAsync());
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", peer);
        var closed = await client.PostAsync(route + "/close", null);
        var requiresReopen = await client.PostAsJsonAsync("/api/billing/invoices", new
        { isDelivery = true, items = new[] { new { productId = product.Id, quantity = 1 } } });
        var reopened = await client.PostAsync(route + "/reopen", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        Assert.Equal(day.Id, invoice.BusinessDayId);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(invoice.Id, detail.GetProperty("invoices")[0].GetProperty("invoiceId").GetGuid());
        Assert.Equal("Cajero", detail.GetProperty("invoices")[0].GetProperty("issuerName").GetString());
        Assert.Equal(HttpStatusCode.OK, collected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, requiresReopen.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        Assert.True(day.IsReopened);
        Assert.Equal(500m, day.Closing!.Cash);
        Assert.Single(days.Days);
    }

    private sealed class Directorio(Guid branch) : IBranchDirectory
    {
        public Task<IReadOnlyList<BranchScope>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<BranchScope?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(id == branch ? new BranchScope(id, "Sucursal", "Dirección", false) : null);
    }
}
