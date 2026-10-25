using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Infrastructure.Persistence;

namespace PirryLedger.Core.Permissions.Tests.Persistencia;

// RF-GP-02: el filtro por solicitante vive en PermissionRequestRepository, no
// en el caso de uso. El resto de las pruebas del modulo usan
// FakePermissionRequestRepository, que repite el mismo predicado: si el
// repositorio real cambia, el doble no se entera y la suite sigue verde.
//
// Por eso esta prueba, como PruebasDeApprovePermissionGrantUnico, ejercita el
// repositorio real sobre un contexto EF Core en memoria, sin PostgreSQL.
public sealed class PruebasDeListadoPorSolicitante
{
    private static PermissionsDbContext ContextoEnMemoria(string nombreBase)
    {
        var options = new DbContextOptionsBuilder<PermissionsDbContext>()
            .UseInMemoryDatabase(nombreBase)
            .Options;

        return new PermissionsDbContext(options);
    }

    [Fact]
    public async Task ListarPorSolicitanteDevuelveSoloLasSolicitudesDeEseSolicitante()
    {
        // Arrange: dos solicitantes distintos, cada uno con su solicitud.
        var solicitanteA = Guid.NewGuid();
        var solicitanteB = Guid.NewGuid();
        var instante = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        await using var contexto = ContextoEnMemoria(
            nameof(ListarPorSolicitanteDevuelveSoloLasSolicitudesDeEseSolicitante));
        var repositorio = new PermissionRequestRepository(contexto);

        await repositorio.SaveAsync(
            PermissionRequest.Create(solicitanteA, "ViewSalesSummary", instante),
            CancellationToken.None);

        await repositorio.SaveAsync(
            PermissionRequest.Create(solicitanteB, "ViewSalesSummary", instante.AddMinutes(1)),
            CancellationToken.None);

        // Act
        var solicitudesDeA = await repositorio.ListByRequesterAsync(solicitanteA, CancellationToken.None);

        // Assert: solo la de A. La de B existe en la tabla, pero no pertenece
        // a quien consulta.
        var unica = Assert.Single(solicitudesDeA);
        Assert.Equal(solicitanteA, unica.RequesterId);
    }
}
