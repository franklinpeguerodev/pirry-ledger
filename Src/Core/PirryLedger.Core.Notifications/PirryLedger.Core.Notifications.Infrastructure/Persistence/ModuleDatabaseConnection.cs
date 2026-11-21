using System.Data.Common;
using Npgsql;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Core.Notifications.Infrastructure.Persistence;

internal sealed class ModuleDatabaseConnection(string connectionString) : IModuleDatabaseConnection
{
    public DbConnection Connection { get; } = new NpgsqlConnection(connectionString);
    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
