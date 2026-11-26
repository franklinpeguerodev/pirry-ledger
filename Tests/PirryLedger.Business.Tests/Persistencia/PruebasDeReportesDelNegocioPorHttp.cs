using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Tests.Imagenes;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Reports;
using PirryLedger.Core.Reports.Application;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDeReportesDelNegocioPorHttp
{
    private const string Rango = "?from=2026-11-24&to=2026-11-25";

    [Theory]
    [InlineData("sales", "&grouping=Day")]
    [InlineData("collections", "")]
    [InlineData("products", "")]
    [InlineData("exclusions", "")]
    [InlineData("employees", "")]
    [InlineData("debts/pending", "")]
    [InlineData("debts/settled", "")]
    [InlineData("closings", "")]
    [InlineData("open-sales", "")]
    public async Task CadaReporteRechazaAnonimoYEstandarAntesDeConsultar(string route, string extra)
    {
        await using var server = new ServidorDePruebasHttp();
        var url = "/api/reports/business/" + route + Rango + extra;

        var anonymous = await server.Cliente.GetAsync(url);
        server.Cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.TokenDeStandard());
        var standard = await server.Cliente.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, standard.StatusCode);
    }

    [Fact]
    public async Task AdministradorConsultaTodosLosReportesConCifrasYCeros()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();
        var read = new Lectura();
        await using var server = new ServidorDePruebasHttp(services =>
        {
            services.AddSingleton<IBusinessReportSource>(fixture.Source);
            services.AddSingleton<IBranchDirectory>(new Directorio(fixture.A, fixture.B));
            services.AddSingleton<IReportReadSession>(read);
            services.AddScoped<GetBusinessReports>();
        });
        var client = server.Cliente;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.TokenDeAdministrator());
        async Task<JsonElement> Get(string route, string extra = "")
        {
            var response = await client.GetAsync("/api/reports/business/" + route + Rango + extra);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
        }

        var sales = await Get("sales", "&grouping=Day");
        Assert.Equal(560m, sales.GetProperty("totals").GetProperty("sold").GetDecimal());
        Assert.Equal(5, sales.GetProperty("totals").GetProperty("invoiceCount").GetInt32());
        Assert.Equal(3, sales.GetProperty("branches").GetArrayLength());
        var emptyBranch = sales.GetProperty("branches").EnumerateArray().Single(branch => branch.GetProperty("branchName").GetString() == "Vacía");
        Assert.Equal(0m, emptyBranch.GetProperty("totals").GetProperty("sold").GetDecimal());
        var selected = await Get("sales", "&grouping=Day&productNames=A&productNames=B");
        Assert.Equal(310m, selected.GetProperty("totals").GetProperty("sold").GetDecimal());
        Assert.False(selected.GetProperty("totals").TryGetProperty("methods", out _));
        var methods = await Get("collections");
        Assert.Equal(300m, methods.GetProperty("totals").GetProperty("cash").GetDecimal());
        Assert.Equal(160m, methods.GetProperty("totals").GetProperty("debt").GetDecimal());
        var products = await Get("products");
        Assert.Equal(5, products.GetProperty("totals").GetProperty("invoiceCount").GetInt32());
        var exclusions = await Get("exclusions");
        Assert.Equal(12.5m, exclusions.GetProperty("totals").GetProperty("pendingCancellationPercentage").GetDecimal());
        var employees = await Get("employees");
        Assert.Equal(3, employees.GetProperty("employees").GetArrayLength());
        var pending = await Get("debts/pending");
        Assert.Equal(100m, pending.GetProperty("page").GetProperty("outstanding").GetDecimal());
        var emptyPage = await Get("debts/pending", "&offset=1&limit=1");
        Assert.Equal(0, emptyPage.GetProperty("page").GetProperty("items").GetArrayLength());
        Assert.Equal(1, emptyPage.GetProperty("page").GetProperty("totalCount").GetInt32());
        Assert.Equal(100m, emptyPage.GetProperty("page").GetProperty("outstanding").GetDecimal());
        var settled = await Get("debts/settled");
        Assert.Equal(1, settled.GetProperty("page").GetProperty("totalCount").GetInt32());
        var closings = await Get("closings", "&limit=1");
        Assert.Equal(3, closings.GetProperty("page").GetProperty("totalCount").GetInt32());
        Assert.Equal(560m, closings.GetProperty("page").GetProperty("closedTotals").GetProperty("sold").GetDecimal());
        var open = await Get("open-sales");
        Assert.Equal(150m, open.GetProperty("sold").GetDecimal());
        var before = read.Calls;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/business/closings" + Rango + "&limit=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/business/sales?from=2026-11-25&to=2026-11-24&grouping=Day")).StatusCode);
        Assert.Equal(before, read.Calls);
    }

    private sealed class Lectura : IReportReadSession
    {
        public int Calls { get; private set; }
        public Task<T> ExecuteAsync<T>(Func<Task<T>> query, CancellationToken ct = default)
        {
            Calls++;
            ct.ThrowIfCancellationRequested();
            return query();
        }
    }
    private sealed class Directorio(Guid a, Guid b) : IBranchDirectory
    {
        private readonly Guid _empty = Guid.NewGuid();
        public Task<IReadOnlyList<BranchScope>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BranchScope>>([new(a, "A", "Dirección", false),
                new(b, "B", "Dirección", false), new(_empty, "Vacía", "Dirección", false)]);
        public Task<BranchScope?> FindAsync(Guid id, CancellationToken ct = default) => Task.FromResult<BranchScope?>(null);
    }
}
