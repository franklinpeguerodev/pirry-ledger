using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Business.Infrastructure.Persistence;

public sealed class BusinessDbContextFactory : IDesignTimeDbContextFactory<BusinessDbContext>
{
    private const string ConnectionVariable = DatabaseConfiguration.ConnectionVariable;

    public BusinessDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno {ConnectionVariable}. El README explica como definirla.");
        }

        var options = new DbContextOptionsBuilder<BusinessDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BusinessDbContext(options);
    }
}
