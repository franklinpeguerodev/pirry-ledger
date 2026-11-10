using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Core.Documents.Infrastructure.Persistence;

// Solo la usa 'dotnet ef'. Permite que esta pieza cree y aplique sus propias
// migraciones sin que el Host sepa nada de Entity Framework (RD-01).
//
// Comparte la cadena de conexion con las otras piezas: una sola base de datos,
// ConnectionStrings__PirryLedger, leida de la variable de entorno que documenta
// el README. Nunca hay una cadena de conexion en el repositorio (RD-10).
public sealed class DocumentDbContextFactory : IDesignTimeDbContextFactory<DocumentDbContext>
{
    private const string ConnectionVariable = DatabaseConfiguration.ConnectionVariable;

    public DocumentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno {ConnectionVariable}. El README explica como definirla.");
        }

        var options = new DbContextOptionsBuilder<DocumentDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DocumentDbContext(options);
    }
}
