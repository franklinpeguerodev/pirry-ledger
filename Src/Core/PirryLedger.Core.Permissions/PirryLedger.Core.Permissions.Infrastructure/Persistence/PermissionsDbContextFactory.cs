using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Core.Permissions.Infrastructure.Persistence;

// Solo la usa 'dotnet ef'. Permite que esta pieza cree y aplique sus propias
// migraciones sin que el Host sepa nada de Entity Framework (RD-01).
//
// Comparte la cadena de conexion con las otras dos piezas: una sola base de
// datos, ConnectionStrings__PirryLedger, leida de la variable de entorno que
// documenta el README (paso 4). Nunca hay una cadena de conexion en el
// repositorio (RD-10), ni siquiera una de ejemplo: con una fija, 'dotnet ef
// database update' apuntaria a otra base que la que usa la aplicacion.
public sealed class PermissionsDbContextFactory : IDesignTimeDbContextFactory<PermissionsDbContext>
{
    private const string ConnectionVariable = DatabaseConfiguration.ConnectionVariable;

    public PermissionsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno {ConnectionVariable}. El README explica como definirla.");
        }

        var options = new DbContextOptionsBuilder<PermissionsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PermissionsDbContext(options);
    }
}