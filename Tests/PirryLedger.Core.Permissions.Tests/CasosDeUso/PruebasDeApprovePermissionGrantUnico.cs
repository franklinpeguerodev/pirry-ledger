using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Infrastructure.Persistence;

namespace PirryLedger.Core.Permissions.Tests.CasosDeUso;

// Cubre la regla de upsert: si el mismo par (user_id, permission_name) ya
// esta en per_permission_grants, una nueva aprobacion re-asigna los sellos de
// tiempo y autor sobre la fila existente, no crea una nueva.
//
// Esto es lo que evita la violacion de unicidad 23505 que se disparaba cuando
// un mismo usuario recibia aprobaciones para el mismo permiso en solicitudes
// distintas (RF-GP-05 + ADR 008).
//
// A diferencia del resto de las pruebas del modulo, esta NO usa el doble en
// memoria: el bug estaba en PermissionGrantRepository, y un doble que implemente
// el upsert probaria el doble, no el repositorio. Aqui se ejercita el
// repositorio real sobre un contexto EF Core en memoria, sin PostgreSQL.
public sealed class PruebasDeApprovePermissionGrantUnico
{
    private static PermissionsDbContext ContextoEnMemoria(string nombreBase)
    {
        var options = new DbContextOptionsBuilder<PermissionsDbContext>()
            .UseInMemoryDatabase(nombreBase)
            .Options;

        return new PermissionsDbContext(options);
    }

    [Fact]
    public async Task AprobarDosVecesElMismoPermisoNoCreaUnaFilaDuplicada()
    {
        // Arrange
        var usuarioId = Guid.NewGuid();
        var aprobadorId = Guid.NewGuid();
        var primerGrant = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var segundoGrant = primerGrant.AddMinutes(5);

        await using var contexto = ContextoEnMemoria(nameof(AprobarDosVecesElMismoPermisoNoCreaUnaFilaDuplicada));
        var repositorio = new PermissionGrantRepository(contexto);

        // Act
        await repositorio.SaveAsync(
            PermissionGrant.Create(usuarioId, "ViewSalesSummary", primerGrant, aprobadorId),
            CancellationToken.None);

        await repositorio.SaveAsync(
            PermissionGrant.Create(usuarioId, "ViewSalesSummary", segundoGrant, aprobadorId),
            CancellationToken.None);

        // Assert
        // Una sola fila: el indice unico (user_id, permission_name) de ADR 008
        // se respeta, y el repositorio reuso la fila existente en vez de
        // intentar un INSERT que la BD rechazaria con 23505.
        var filas = await contexto.PermissionGrants
            .Where(grant => grant.UserId == usuarioId)
            .ToListAsync();

        Assert.Single(filas);
        Assert.Equal("ViewSalesSummary", filas[0].PermissionName);
    }

    [Fact]
    public async Task AprobarDosVecesElMismoPermisoReasignaElSelloDeTiempo()
    {
        // Arrange
        var usuarioId = Guid.NewGuid();
        var aprobadorId = Guid.NewGuid();
        var primerGrant = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var segundoGrant = primerGrant.AddMinutes(5);

        await using var contexto = ContextoEnMemoria(nameof(AprobarDosVecesElMismoPermisoReasignaElSelloDeTiempo));
        var repositorio = new PermissionGrantRepository(contexto);

        // Act
        await repositorio.SaveAsync(
            PermissionGrant.Create(usuarioId, "ViewSalesSummary", primerGrant, aprobadorId),
            CancellationToken.None);

        await repositorio.SaveAsync(
            PermissionGrant.Create(usuarioId, "ViewSalesSummary", segundoGrant, aprobadorId),
            CancellationToken.None);

        // Assert
        // La fila existente se actualizo: el sello de tiempo es el de la
        // segunda aprobacion, no el de la primera. Sin el Regrant, la fila
        // habria seguido con el sello viejo (o el INSERT habria fallado).
        var fila = await contexto.PermissionGrants
            .SingleAsync(grant => grant.UserId == usuarioId);

        Assert.Equal(segundoGrant, fila.GrantedAtUtc);
    }

    [Fact]
    public async Task AprobarDosPermisosDistintosDelMismoUsuarioCreaDosFilas()
    {
        // Arrange: el upsert es por par (user_id, permission_name). Un
        // permiso distinto del mismo usuario es otra concesion y va en otra
        // fila.
        var usuarioId = Guid.NewGuid();
        var aprobadorId = Guid.NewGuid();
        var instante = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

        await using var contexto = ContextoEnMemoria(nameof(AprobarDosPermisosDistintosDelMismoUsuarioCreaDosFilas));
        var repositorio = new PermissionGrantRepository(contexto);

        // Act
        await repositorio.SaveAsync(
            PermissionGrant.Create(usuarioId, "ViewSalesSummary", instante, aprobadorId),
            CancellationToken.None);

        await repositorio.SaveAsync(
            PermissionGrant.Create(usuarioId, "OtroPermiso", instante, aprobadorId),
            CancellationToken.None);

        // Assert
        var filas = await contexto.PermissionGrants
            .Where(grant => grant.UserId == usuarioId)
            .ToListAsync();

        Assert.Equal(2, filas.Count);
    }
}