using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Reports;
using PirryLedger.Core.Reports.Application;
using PirryLedger.Core.Reports.Domain;

namespace PirryLedger.Core.Reports.Tests;

public sealed class PruebasDelReporteDePermisos
{
    [Fact]
    public void PeriodoIncluyeDiasCompletosEnHoraDeRd()
    {
        var period = new ReportPeriod(new(2026, 11, 1), new(2026, 11, 2));

        var (from, until) = period.ToUtc();

        Assert.Equal(new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2026, 11, 3, 4, 0, 0, DateTimeKind.Utc), until);
    }

    [Fact]
    public async Task SucursalesYCerosConservanConteoUnicoPorEstado()
    {
        var directory = new Directorio();
        var source = new Origen([new(directory.First.Id, PermissionReportState.Applied, 3),
            new(directory.Second.Id, PermissionReportState.Pending, 2)]);
        var report = new GetPermissionRequestReport(source, directory, Administrador());

        var result = await report.ExecuteAsync(new(2026, 11, 1), new(2026, 11, 2), []);

        Assert.Equal(5, result.Counts.Total);
        Assert.Equal(3, result.Counts.Applied);
        Assert.Equal(0, result.Counts.Approved);
        Assert.Equal(0, result.Counts.Rejected);
        Assert.Equal(2, result.Branches.Count);
        Assert.Equal(5, result.Branches.Sum(branch => branch.Counts.Total));
    }

    [Fact]
    public async Task SeleccionFisicaSeEnvíaAlOrigenAntesDeAgregar()
    {
        var directory = new Directorio();
        var source = new Origen([]);
        var report = new GetPermissionRequestReport(source, directory, Administrador());

        var result = await report.ExecuteAsync(new(2026, 11, 1), new(2026, 11, 2), [directory.First.Id]);

        Assert.Equal(directory.First.Id, Assert.Single(source.Selected));
        Assert.Single(result.Branches);
        Assert.Equal(0, result.Counts.Total);
    }

    [Fact]
    public async Task EstandarSeRechazaAntesDeConsultarDatos()
    {
        var source = new Origen([]);
        var report = new GetPermissionRequestReport(source, new Directorio(),
            new Alcance { UserId = Guid.NewGuid(), IsGlobal = false });

        await Assert.ThrowsAsync<ReportAccessDeniedException>(() => report.ExecuteAsync(new(2026, 11, 1), new(2026, 11, 2), []));

        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task RangoInvertidoSeRechazaSinConsultarOrigen()
    {
        var source = new Origen([]);
        var report = new GetPermissionRequestReport(source, new Directorio(), Administrador());

        await Assert.ThrowsAsync<ArgumentException>(() => report.ExecuteAsync(new(2026, 11, 2), new(2026, 11, 1), []));

        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task SucursalDesconocidaSeRechazaSinConsultarOrigen()
    {
        var source = new Origen([]);
        var report = new GetPermissionRequestReport(source, new Directorio(), Administrador());

        await Assert.ThrowsAsync<ArgumentException>(() => report.ExecuteAsync(new(2026, 11, 1), new(2026, 11, 2), [Guid.NewGuid()]));

        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task TodasIncluyeAdministrativasUnaVezYConservaSucursalesSinActividad()
    {
        var directory = new Directorio(true);
        var source = new Origen([new(directory.Global.Id, PermissionReportState.Applied, 1),
            new(directory.First.Id, PermissionReportState.Pending, 2)]);
        var report = new GetPermissionRequestReport(source, directory, Administrador());

        var result = await report.ExecuteAsync(new(2026, 11, 1), new(2026, 11, 2), [directory.Global.Id]);

        Assert.Equal(3, result.Counts.Total);
        Assert.Equal(3, result.Branches.Count);
        Assert.Equal(1, result.Branches.Single(branch => branch.BranchId == directory.Global.Id).Counts.Total);
        Assert.Equal(0, result.Branches.Single(branch => branch.BranchId == directory.Second.Id).Counts.Total);
        Assert.Equal(3, result.Branches.Sum(branch => branch.Counts.Total));
    }

    private static Alcance Administrador() => new() { UserId = Guid.NewGuid(), IsGlobal = true };
    private sealed class Alcance : IBranchScopeAccessor
    {
        public Guid? BranchId { get; set; }
        public Guid? UserId { get; set; }
        public bool IsGlobal { get; set; }
    }
    private sealed class Origen(IReadOnlyList<PermissionRequestCount> counts) : IPermissionRequestReportSource
    {
        public int Calls { get; private set; }
        public IReadOnlyCollection<Guid> Selected { get; private set; } = [];
        public Task<IReadOnlyList<PermissionRequestCount>> AggregateAsync(DateTime from, DateTime until,
            IReadOnlyCollection<Guid> branches, CancellationToken cancellationToken = default)
        {
            Calls++;
            Selected = branches;
            return Task.FromResult(counts);
        }
    }
    private sealed class Directorio(bool includeGlobal = false) : IBranchDirectory
    {
        public BranchScope Global { get; } = new(Guid.NewGuid(), "Todas", "Todas", true);
        public BranchScope First { get; } = new(Guid.NewGuid(), "A", "Dirección", false);
        public BranchScope Second { get; } = new(Guid.NewGuid(), "B", "Dirección", false);
        public Task<IReadOnlyList<BranchScope>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BranchScope>>(includeGlobal ? [Global, First, Second] : [First, Second]);
        public Task<BranchScope?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<BranchScope?>(null);
    }
}
