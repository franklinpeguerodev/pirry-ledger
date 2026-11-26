using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Api.Models;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Infrastructure.Persistence;
using PirryLedger.Business.Tests.Imagenes;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDePropiedadPorHttp
{
    [Fact]
    public async Task LaSesionFijaElEmisorYUnCompaneroNoLeeNiConcluyeSuFactura()
    {
        // Arrange: rutas, filtro y repositorio reales; persistencia en memoria.
        var database = Guid.NewGuid().ToString();
        var branch = Guid.NewGuid();
        await using var server = new ServidorDePruebasHttp(services =>
        {
            services.AddDbContext<BusinessDbContext>(options => options.UseInMemoryDatabase(database));
            services.AddScoped<IInvoiceRepository, InvoiceRepository>();
            services.AddScoped<IssueInvoice>();
            services.AddScoped<ListInvoices>();
            services.AddScoped<GetInvoice>();
            services.AddScoped<MarkInvoiceAsNotDelivered>();
            services.AddSingleton<IBranchDirectory>(new SucursalDePrueba(branch));
        });
        var ownerToken = server.TokenDeStandard("ana@ejemplo.com", branch);
        var otherToken = server.TokenDeStandard("juan@ejemplo.com", branch);
        var product = server.CrearProducto();
        var client = server.Cliente;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // Act
        var created = await client.PostAsJsonAsync("/api/billing/invoices", new
        {
            isDelivery = true,
            issuedByUserId = Guid.NewGuid(),
            branchId = Guid.NewGuid(),
            items = new[] { new { productId = product.Id, quantity = 1 } }
        });
        var invoice = (await created.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        var ownList = await client.GetFromJsonAsync<InvoiceResponse[]>("/api/billing/invoices");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var otherList = await client.GetFromJsonAsync<InvoiceResponse[]>("/api/billing/invoices");
        var read = await client.GetAsync($"/api/billing/invoices/{invoice.Id}");
        var conclude = await client.PostAsync($"/api/billing/invoices/{invoice.Id}/not-delivered", null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var unchanged = (await client.GetFromJsonAsync<InvoiceResponse>($"/api/billing/invoices/{invoice.Id}"))!;

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(branch, invoice.BranchId);
        var owner = await server.Usuarios.FindByEmailAsync("ana@ejemplo.com");
        Assert.Equal(owner!.Id, invoice.IssuedByUserId);
        Assert.Single(ownList!);
        Assert.Empty(otherList!);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, conclude.StatusCode);
        Assert.Equal("Issued", unchanged.State);
        Assert.Empty(unchanged.Payments);
    }

    private sealed class SucursalDePrueba(Guid id) : IBranchDirectory
    {
        public Task<IReadOnlyList<BranchScope>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<BranchScope?> FindAsync(Guid branchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(branchId == id ? new BranchScope(id, "Sucursal", "Dirección", false) : null);
    }
}
