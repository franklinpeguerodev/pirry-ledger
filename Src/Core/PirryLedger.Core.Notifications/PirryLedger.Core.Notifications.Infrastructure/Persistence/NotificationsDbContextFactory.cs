using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Core.Notifications.Infrastructure;

// Solo la usa 'dotnet ef'. Permite que cada modulo cree y aplique sus propias
// migraciones sin que el Host sepa nada de Entity Framework: el Host no declara
// ningun DbContext (RD-01).
//
// Las dos piezas comparten base de datos, asi que comparten la misma cadena de
// conexion: ConnectionStrings__PirryLedger, en la variable de entorno que el
// README documenta por Name. Nunca hay una cadena de conexion en el
// repository (RD-10).
public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    private const string ConnectionVariable = DatabaseConfiguration.ConnectionVariable;

    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno {ConnectionVariable}. El README explica como definirla.");
        }

        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new NotificationsDbContext(options);
    }
}
