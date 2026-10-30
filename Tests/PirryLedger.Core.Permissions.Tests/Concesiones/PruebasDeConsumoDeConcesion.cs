using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Infrastructure.Persistence;
using PirryLedger.Core.Permissions.Infrastructure.Security;

namespace PirryLedger.Core.Permissions.Tests.Concesiones;

// RF-GP-05 + RF-NEG-06: consumir una concesion es un borrado de una sola vez.
// La prueba ejercita el repositorio real sobre EF InMemory (sin PostgreSQL),
// igual que PruebasDeApprovePermissionGrantUnico, porque la regla vive en el
// repositorio y un doble probaria el doble.
public sealed class PruebasDeConsumoDeConcesion
{
    private static readonly DateTime Ahora = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private static PermissionsDbContext ContextoEnMemoria(string nombreBase)
    {
        var options = new DbContextOptionsBuilder<PermissionsDbContext>()
            .UseInMemoryDatabase(nombreBase)
            .Options;

        return new PermissionsDbContext(options);
    }

    private static PermissionGrant Concesion(Guid usuarioId, string permiso) =>
        PermissionGrant.Create(usuarioId, permiso, Ahora, grantedByUserId: null);

    [Fact]
    public async Task ConsumirUnaConcesionLaBorraYDevuelveTrue()
    {
        // Arrange
        var usuarioId = Guid.NewGuid();
        var permiso = PermissionNames.CancelInvoice(Guid.NewGuid());

        await using var contexto = ContextoEnMemoria(nameof(ConsumirUnaConcesionLaBorraYDevuelveTrue));
        var repositorio = new PermissionGrantRepository(contexto);
        await repositorio.SaveAsync(Concesion(usuarioId, permiso), CancellationToken.None);

        // Act
        var consumida = await repositorio.ConsumeAsync(usuarioId, permiso, CancellationToken.None);

        // Assert
        Assert.True(consumida);
        Assert.False(await contexto.PermissionGrants.AnyAsync(
            grant => grant.UserId == usuarioId && grant.PermissionName == permiso));
    }

    [Fact]
    public async Task ConsumirDosVecesLaMismaConcesionLaSegundaDevuelveFalse()
    {
        // Arrange
        var usuarioId = Guid.NewGuid();
        var permiso = PermissionNames.CancelInvoice(Guid.NewGuid());

        await using var contexto = ContextoEnMemoria(nameof(ConsumirDosVecesLaMismaConcesionLaSegundaDevuelveFalse));
        var repositorio = new PermissionGrantRepository(contexto);
        await repositorio.SaveAsync(Concesion(usuarioId, permiso), CancellationToken.None);

        // Act
        var primera = await repositorio.ConsumeAsync(usuarioId, permiso, CancellationToken.None);
        var segunda = await repositorio.ConsumeAsync(usuarioId, permiso, CancellationToken.None);

        // Assert: una sola vez; la segunda no encuentra nada y no falla.
        Assert.True(primera);
        Assert.False(segunda);
        Assert.Empty(await contexto.PermissionGrants.ToListAsync());
    }

    [Fact]
    public async Task ConsumirLaConcesionDeUnaFacturaNoBorraLaDeOtra()
    {
        // Arrange: dos facturas distintas, dos permisos distintos.
        var usuarioId = Guid.NewGuid();
        var primeraFactura = PermissionNames.CancelInvoice(Guid.NewGuid());
        var segundaFactura = PermissionNames.CancelInvoice(Guid.NewGuid());

        await using var contexto = ContextoEnMemoria(nameof(ConsumirLaConcesionDeUnaFacturaNoBorraLaDeOtra));
        var repositorio = new PermissionGrantRepository(contexto);
        await repositorio.SaveAsync(Concesion(usuarioId, primeraFactura), CancellationToken.None);
        await repositorio.SaveAsync(Concesion(usuarioId, segundaFactura), CancellationToken.None);

        // Act
        var consumida = await repositorio.ConsumeAsync(usuarioId, primeraFactura, CancellationToken.None);

        // Assert
        Assert.True(consumida);
        Assert.True(await contexto.PermissionGrants.AnyAsync(
            grant => grant.UserId == usuarioId && grant.PermissionName == segundaFactura));
    }

    [Fact]
    public async Task ConsumirLaConcesionDeOtroUsuarioNoLaBorra()
    {
        // Arrange: el mismo permiso para dos usuarios. Cada uno pide el suyo.
        var usuarioA = Guid.NewGuid();
        var usuarioB = Guid.NewGuid();
        var permiso = PermissionNames.CancelInvoice(Guid.NewGuid());

        await using var contexto = ContextoEnMemoria(nameof(ConsumirLaConcesionDeOtroUsuarioNoLaBorra));
        var repositorio = new PermissionGrantRepository(contexto);
        await repositorio.SaveAsync(Concesion(usuarioA, permiso), CancellationToken.None);
        await repositorio.SaveAsync(Concesion(usuarioB, permiso), CancellationToken.None);

        // Act
        var consumida = await repositorio.ConsumeAsync(usuarioA, permiso, CancellationToken.None);

        // Assert
        Assert.True(consumida);
        Assert.True(await contexto.PermissionGrants.AnyAsync(
            grant => grant.UserId == usuarioB && grant.PermissionName == permiso));
    }

    [Fact]
    public async Task ConsumirSinConcesionDevuelveFalseYNoCambiaNada()
    {
        // Arrange: no hay ninguna concesion.
        await using var contexto = ContextoEnMemoria(nameof(ConsumirSinConcesionDevuelveFalseYNoCambiaNada));
        var repositorio = new PermissionGrantRepository(contexto);

        // Act
        var consumida = await repositorio.ConsumeAsync(
            Guid.NewGuid(),
            PermissionNames.CancelInvoice(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        Assert.False(consumida);
        Assert.Empty(await contexto.PermissionGrants.ToListAsync());
    }

    [Fact]
    public async Task LaAutoridadDejaDeVerElPermisoDespuesDeConsumirlo()
    {
        // Arrange
        var usuarioId = Guid.NewGuid();
        var permiso = PermissionNames.CancelInvoice(Guid.NewGuid());

        await using var contexto = ContextoEnMemoria(nameof(LaAutoridadDejaDeVerElPermisoDespuesDeConsumirlo));
        var repositorio = new PermissionGrantRepository(contexto);
        var autoridad = new EfPermissionAuthority(repositorio);
        await repositorio.SaveAsync(Concesion(usuarioId, permiso), CancellationToken.None);

        // Act
        var antes = await autoridad.UserHasPermissionAsync(usuarioId, permiso, CancellationToken.None);
        var consumida = await autoridad.ConsumeAsync(usuarioId, permiso, CancellationToken.None);
        var despues = await autoridad.UserHasPermissionAsync(usuarioId, permiso, CancellationToken.None);

        // Assert
        Assert.True(antes);
        Assert.True(consumida);
        Assert.False(despues);
    }

    [Fact]
    public async Task LaAutoridadNoConsumeConUsuarioVacio()
    {
        // Arrange
        var usuarioId = Guid.NewGuid();
        var permiso = PermissionNames.CancelInvoice(Guid.NewGuid());

        await using var contexto = ContextoEnMemoria(nameof(LaAutoridadNoConsumeConUsuarioVacio));
        var repositorio = new PermissionGrantRepository(contexto);
        var autoridad = new EfPermissionAuthority(repositorio);
        await repositorio.SaveAsync(Concesion(usuarioId, permiso), CancellationToken.None);

        // Act
        var consumida = await autoridad.ConsumeAsync(Guid.Empty, permiso, CancellationToken.None);

        // Assert: la concesion sigue viva.
        Assert.False(consumida);
        Assert.True(await contexto.PermissionGrants.AnyAsync(
            grant => grant.UserId == usuarioId && grant.PermissionName == permiso));
    }
}
