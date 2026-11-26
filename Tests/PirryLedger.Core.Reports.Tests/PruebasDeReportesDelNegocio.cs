using System.Text.Json;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Reports;
using PirryLedger.Core.Reports.Application;

namespace PirryLedger.Core.Reports.Tests;

public sealed class PruebasDeReportesDelNegocio
{
    private static readonly DateOnly Desde = new(2026, 11, 24);
    private static readonly DateOnly Hasta = new(2026, 11, 25);

    [Fact]
    public async Task RespuestaVaciaConservaSucursalesYCeros()
    {
        var scenario = new Escenario();

        var report = await scenario.Reports.SalesAsync(Desde, Hasta, [], [], SalesReportGrouping.Day);

        Assert.Equal(2, report.Branches.Count);
        Assert.All(report.Branches, branch => Assert.Equal(0m, branch.Totals.Sold));
        Assert.Empty(report.Periods);
        Assert.Equal(0, report.Totals.InvoiceCount);
        Assert.Equal(0m, report.Totals.Methods!.Cash);
        Assert.Equal(1, scenario.Read.Calls);
        Assert.False(scenario.Read.Active);
    }

    [Fact]
    public async Task SubconjuntoDeProductosOmiteMetodosInclusoSinFilas()
    {
        var scenario = new Escenario();

        var report = await scenario.Reports.SalesAsync(Desde, Hasta, [], ["Combo", "Combo", "combo"], SalesReportGrouping.Day);

        Assert.Null(report.Totals.Methods);
        Assert.All(report.Branches, branch => Assert.Null(branch.Totals.Methods));
        Assert.Equal(new[] { "Combo", "combo" }, scenario.Source.Filter!.ProductNames);
        var serialized = JsonSerializer.SerializeToElement(report, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(serialized.GetProperty("totals").TryGetProperty("methods", out _));
    }

    [Fact]
    public async Task TotalesDeProductosUsanFacturasDistintasYNoSumaDeConteosPorProducto()
    {
        var scenario = new Escenario();
        scenario.Source.Products = new([new(scenario.Directory.A, "A", 1, 100m, 1),
            new(scenario.Directory.A, "B", 1, 50m, 1)], [new(scenario.Directory.A, 1)]);

        var report = await scenario.Reports.ProductsAsync(Desde, Hasta, [], []);

        Assert.Equal(1, report.Totals.InvoiceCount);
        Assert.Equal(2, report.Products.Sum(row => row.InvoiceCount));
        Assert.Equal(150m, report.Totals.Amount);
        Assert.Equal(2L, report.Totals.Units);
    }

    [Fact]
    public async Task PorcentajeUsaFacturasDelFiltroCompleto()
    {
        var scenario = new Escenario();
        scenario.Source.Exclusions = [new(scenario.Directory.A, 8, new(1, 60m), new(1, 70m), new(1, 50m))];

        var report = await scenario.Reports.ExclusionsAsync(Desde, Hasta, []);

        Assert.Equal(12.5m, report.Totals.PendingCancellationPercentage);
        Assert.Equal(12.5m, report.Totals.CancelledPercentage);
        Assert.Equal(8, report.Totals.Values.IssuedCount);
    }

    [Fact]
    public async Task PorcentajesSinFacturasSonCero()
    {
        var scenario = new Escenario();

        var report = await scenario.Reports.ExclusionsAsync(Desde, Hasta, []);

        Assert.Equal(0m, report.Totals.PendingCancellationPercentage);
        Assert.Equal(0m, report.Totals.CancelledPercentage);
    }

    [Fact]
    public async Task DeudasSaldadasYPendientesSeConsultanIndependientemente()
    {
        var scenario = new Escenario();

        await scenario.Reports.DebtsAsync(Desde, Hasta, [], false, new());
        await scenario.Reports.DebtsAsync(Desde, Hasta, [], true, new());

        Assert.Equal(new[] { false, true }, scenario.Source.DebtKinds);
        Assert.Equal(2, scenario.Read.Calls);
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task PaginacionInvalidaRechazaSinAbrirLectura(int offset, int limit)
    {
        var scenario = new Escenario();

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.Reports.DebtsAsync(Desde, Hasta, [], false, new(offset, limit)));

        Assert.Equal(0, scenario.Source.Calls);
        Assert.Equal(0, scenario.Read.Calls);
    }

    [Fact]
    public async Task EstándarNoConsultaNingunDato()
    {
        var scenario = new Escenario();
        scenario.Scope.IsGlobal = false;

        await Assert.ThrowsAsync<ReportAccessDeniedException>(() => scenario.Reports.OpenSalesAsync([]));
        await Assert.ThrowsAsync<ReportAccessDeniedException>(() => scenario.Reports.SalesAsync(Desde, Hasta, [], [], SalesReportGrouping.Day));

        Assert.Equal(0, scenario.Source.Calls);
        Assert.Equal(0, scenario.Read.Calls);
    }

    [Fact]
    public async Task SucursalDesconocidaSeRechazaSinConsultarBilling()
    {
        var scenario = new Escenario();

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.Reports.ProductsAsync(Desde, Hasta, [Guid.NewGuid()], []));

        Assert.Equal(0, scenario.Source.Calls);
        Assert.False(scenario.Read.Active);
    }

    [Fact]
    public async Task RangoInvertidoSeRechazaAntesDeLaLectura()
    {
        var scenario = new Escenario();

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.Reports.MethodsAsync(Hasta, Desde, []));

        Assert.Equal(0, scenario.Read.Calls);
        Assert.Equal(0, scenario.Source.Calls);
    }

    [Fact]
    public async Task TodasSeleccionaSucursalesFisicasSinCrearUnaSucursalGlobalDeVentas()
    {
        var scenario = new Escenario();

        var report = await scenario.Reports.MethodsAsync(Desde, Hasta, [scenario.Directory.Global]);

        Assert.Equal(2, report.Branches.Count);
        Assert.DoesNotContain(scenario.Directory.Global, scenario.Source.Filter!.BranchIds);
        Assert.Contains(scenario.Directory.A, scenario.Source.Filter.BranchIds);
    }

    [Fact]
    public async Task CoreSinProveedorBillingConservaUnRechazoControladoSinAbrirLectura()
    {
        var scenario = new Escenario();
        var reports = new GetBusinessReports(scenario.Directory, scenario.Scope, scenario.Read);

        await Assert.ThrowsAsync<BusinessReportsUnavailableException>(() => reports.OpenSalesAsync([]));

        Assert.Equal(0, scenario.Read.Calls);
    }

    private sealed class Escenario
    {
        public Lectura Read { get; } = new();
        public Directorio Directory { get; }
        public Alcance Scope { get; } = new();
        public Origen Source { get; } = new();
        public GetBusinessReports Reports { get; }
        public Escenario()
        {
            Directory = new(Read);
            Reports = new(Directory, Scope, Read, Source);
        }
    }
    private sealed class Alcance : IBranchScopeAccessor
    {
        public Guid? BranchId { get; set; }
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public bool IsGlobal { get; set; } = true;
    }
    private sealed class Lectura : IReportReadSession
    {
        public int Calls { get; private set; }
        public bool Active { get; private set; }
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> query, CancellationToken ct = default)
        {
            Calls++;
            Active = true;
            try { return await query(); }
            finally { Active = false; }
        }
    }
    private sealed class Directorio(Lectura read) : IBranchDirectory
    {
        public Guid A { get; } = Guid.NewGuid();
        public Guid B { get; } = Guid.NewGuid();
        public Guid Global { get; } = Guid.NewGuid();
        public Task<IReadOnlyList<BranchScope>> ListAsync(CancellationToken ct = default)
        {
            Assert.True(read.Active);
            return Task.FromResult<IReadOnlyList<BranchScope>>([new(A, "A", "Dirección", false),
                new(B, "B", "Dirección", false), new(Global, "Todas", "Todas", true)]);
        }
        public Task<BranchScope?> FindAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Origen : IBusinessReportSource
    {
        public int Calls { get; private set; }
        public BusinessReportFilter? Filter { get; private set; }
        public ProductReportData Products { get; set; } = new([], []);
        public IReadOnlyList<ExclusionReportRow> Exclusions { get; set; } = [];
        public List<bool> DebtKinds { get; } = [];
        private Task<T> Result<T>(BusinessReportFilter filter, T value)
        {
            Calls++;
            Filter = filter;
            return Task.FromResult(value);
        }
        public Task<IReadOnlyList<SalesReportRow>> SalesAsync(BusinessReportFilter filter, SalesReportGrouping grouping, CancellationToken ct) =>
            Result<IReadOnlyList<SalesReportRow>>(filter, []);
        public Task<IReadOnlyList<MethodReportRow>> MethodsAsync(BusinessReportFilter filter, CancellationToken ct) =>
            Result<IReadOnlyList<MethodReportRow>>(filter, []);
        public Task<ProductReportData> ProductsAsync(BusinessReportFilter filter, CancellationToken ct) => Result(filter, Products);
        public Task<IReadOnlyList<ExclusionReportRow>> ExclusionsAsync(BusinessReportFilter filter, CancellationToken ct) => Result(filter, Exclusions);
        public Task<IReadOnlyList<EmployeeReportRow>> EmployeesAsync(BusinessReportFilter filter, CancellationToken ct) =>
            Result<IReadOnlyList<EmployeeReportRow>>(filter, []);
        public Task<DebtReportPage> DebtsAsync(BusinessReportFilter filter, bool settled, ReportPage page, CancellationToken ct)
        {
            DebtKinds.Add(settled);
            return Result(filter, new DebtReportPage([], 0, 0, 0, page.Offset, page.Limit, []));
        }
        public Task<ClosingReportPage> ClosingsAsync(BusinessReportFilter filter, ReportPage page, CancellationToken ct) =>
            Result(filter, new ClosingReportPage([], 0, 0, 0, new(0, 0, 0, 0, 0, 0, 0, 0), page.Offset, page.Limit, []));
        public Task<IReadOnlyList<OpenSalesReportRow>> OpenSalesAsync(IReadOnlyList<Guid> branches, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<OpenSalesReportRow>>([]);
        }
    }
}
