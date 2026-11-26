using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Tests.Imagenes;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Reports;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDelReporteCorePorHttp
{
    [Fact]
    public async Task RutaExigeSesionAdministradorYRangoCompleto()
    {
        var source = new Origen();
        await using var server = new ServidorDePruebasHttp(services =>
        {
            services.AddSingleton<IPermissionRequestReportSource>(source);
            services.AddSingleton<IBranchDirectory>(new Directorio());
        });
        var client = server.Cliente;
        var url = "/api/reports/permission-requests?from=2026-11-01&to=2026-11-02";

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.TokenDeStandard());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(0, source.Calls);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.TokenDeAdministrator());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(1, source.Calls);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url + "&branchIds=" + Directorio.BranchId)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url + "&branchIds=" + Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/permission-requests?from=2026-11-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/permission-requests?from=2026-11-02&to=2026-11-01")).StatusCode);
        Assert.Equal(2, source.Calls);
    }
    private sealed class Origen : IPermissionRequestReportSource
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<PermissionRequestCount>> AggregateAsync(DateTime from, DateTime until,
            IReadOnlyCollection<Guid> branches, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<PermissionRequestCount>>([]);
        }
    }
    private sealed class Directorio : IBranchDirectory
    {
        public static readonly Guid BranchId = Guid.NewGuid();
        public Task<IReadOnlyList<BranchScope>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BranchScope>>([new(BranchId, "Sucursal", "Dirección", false)]);
        public Task<BranchScope?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<BranchScope?>(null);
    }
}
