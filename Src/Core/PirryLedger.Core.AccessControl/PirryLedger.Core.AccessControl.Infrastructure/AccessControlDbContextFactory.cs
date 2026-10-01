using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Core.AccessControl.Infrastructure;

// Solo la usa 'dotnet ef'. Permite que esta pieza cree y aplique sus propias
// migraciones sin que el Host sepa nada de Entity Framework (RD-01).
//
// Comparte la cadena de conexion con la pieza de notificaciones: una sola base
// de datos, ConnectionStrings__PirryLedger, leida de la variable de entorno que
// documenta el README. Nunca hay una cadena de conexion en el repositorio
// (RD-10).
public sealed class AccessControlDbContextFactory : IDesignTimeDbContextFactory<AccessControlDbContext>
{
    private const string VariableDeConexion = ConfiguracionDeBaseDeDatos.VariableDeConexion;

    public AccessControlDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(VariableDeConexion);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno {VariableDeConexion}. El README explica como definirla.");
        }

        var opciones = new DbContextOptionsBuilder<AccessControlDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AccessControlDbContext(opciones);
    }
}