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
// README documenta por nombre. Nunca hay una cadena de conexion en el
// repositorio (RD-10).
public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    private const string VariableDeConexion = ConfiguracionDeBaseDeDatos.VariableDeConexion;

    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(VariableDeConexion);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno {VariableDeConexion}. El README explica como definirla.");
        }

        var opciones = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new NotificationsDbContext(opciones);
    }
}
