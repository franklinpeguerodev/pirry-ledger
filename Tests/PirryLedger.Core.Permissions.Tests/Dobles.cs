using PirryLedger.Core.Contracts.Permissions;

namespace PirryLedger.Core.Permissions.Tests;

// RD-12: las pruebas del modulo viven sin base de datos, sin SMTP, sin
// levantar la aplicacion. Todo lo que tocan las pruebas se sustituye por un
// doble. Aqui solo vive lo que las pruebas del estado y de la entidad necesitan.
//
// Las pruebas que usen EF Core directamente tienen su propio setup: usan
// UseInMemoryDatabase (que ya viene con Microsoft.EntityFrameworkCore) o un
// double de IPermissionRequestRepository. Esta clase es solo para las pruebas
// puramente de dominio.

// Catalogo falso para las pruebas: IsKnown devuelve true solo para nombres
// declarados aqui, sin tocar PermissionCatalog (que es el catalogo real de
// produccion).
internal sealed class FakePermissionCatalog : IPermissionAuthority
{
    private readonly HashSet<string> _known;

    public FakePermissionCatalog(IEnumerable<string> known)
    {
        _known = new HashSet<string>(known, StringComparer.Ordinal);
    }

    public FakePermissionCatalog(params string[] known) : this((IEnumerable<string>)known)
    {
    }

    public bool IsKnown(string permissionName) =>
        !string.IsNullOrWhiteSpace(permissionName) && _known.Contains(permissionName);

    public Task<bool> UserHasPermissionAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(IsKnown(permissionName) && userId != Guid.Empty);
}