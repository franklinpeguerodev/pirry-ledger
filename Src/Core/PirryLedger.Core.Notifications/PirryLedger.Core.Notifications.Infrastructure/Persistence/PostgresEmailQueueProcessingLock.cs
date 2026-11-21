using Npgsql;
using PirryLedger.Core.Notifications.Application;

namespace PirryLedger.Core.Notifications.Infrastructure;

public sealed class PostgresEmailQueueProcessingLock(string connectionString) : IEmailQueueProcessingLock
{
    private const long LockKey = 530031;

    public async Task<IEmailQueueProcessingLease?> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        // Una transacción exclusiva conserva el backend incluso con pooling externo.
        var settings = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
        var connection = new NpgsqlConnection(settings.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(@key)", connection, transaction);
            command.Parameters.AddWithValue("key", LockKey);
            if ((bool)(await command.ExecuteScalarAsync(cancellationToken))!) return new Lease(connection, transaction);
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
            return null;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Lease(NpgsqlConnection connection, NpgsqlTransaction transaction) : IEmailQueueProcessingLease
    {
        public async Task VerifyAsync(CancellationToken cancellationToken = default)
        {
            await using var command = new NpgsqlCommand("SELECT 1", connection, transaction);
            await command.ExecuteScalarAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            try { await transaction.DisposeAsync(); }
            finally { await connection.DisposeAsync(); }
        }
    }
}
