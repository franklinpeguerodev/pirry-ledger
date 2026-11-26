using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

public sealed class PruebasDeDestinatariosActivos
{
    private static readonly DateTime Ahora = new(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListaSoloAdministradoresActivosSinExigirSesionAbierta()
    {
        var users = new FakeUserRepository();
        var active = users.Create("Activo", "activo@ejemplo.com", "hash-de-prueba", Ahora, true, Role.Administrator);
        users.Create("Inactivo", "inactivo@ejemplo.com", "hash-de-prueba", Ahora, false, Role.Administrator);
        users.Create("Estándar", "standard@ejemplo.com", "hash-de-prueba", Ahora, true);
        var directory = new NotificationRecipientDirectory(users);

        var result = await directory.ListActiveAdministratorsAsync();

        Assert.Equal(active.Id, Assert.Single(result).UserId);
        Assert.Equal(active.Name, result[0].Name);
        Assert.True(result[0].IsAdministrator);
    }

    [Fact]
    public async Task DestinatarioIndividualReflejaRolActualYDesactivacion()
    {
        var users = new FakeUserRepository();
        var user = users.Create("Usuario", "usuario@ejemplo.com", "hash-de-prueba", Ahora, true);
        var directory = new NotificationRecipientDirectory(users);
        var before = await directory.FindActiveAsync(user.Id);
        user.ChangeRole(Role.Administrator);
        var promoted = await directory.FindActiveAsync(user.Id);

        user.Deactivate();
        var inactive = await directory.FindActiveAsync(user.Id);

        Assert.False(before!.IsAdministrator);
        Assert.True(promoted!.IsAdministrator);
        Assert.Null(inactive);
        Assert.Null(await directory.FindActiveAsync(Guid.NewGuid()));
    }
}
