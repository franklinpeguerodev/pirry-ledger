using System.Data.Common;
using Npgsql;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Core.Notifications.Infrastructure.Persistence;

// Coordinación física compartida; no conoce tablas ni entidades de los módulos.
public sealed class PostgresApplicationTransaction : IApplicationTransaction
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly Func<IReadOnlyList<IModuleTransactionParticipant>> _participants;
    private bool _active;
    private bool _rollbackRequired;
    private readonly bool _ownsConnection;

    public PostgresApplicationTransaction(string connectionString,
        Func<IReadOnlyList<IModuleTransactionParticipant>> participants)
        : this(() => new NpgsqlConnection(connectionString), participants)
    {
    }

    public PostgresApplicationTransaction(Func<DbConnection> connectionFactory,
        Func<IReadOnlyList<IModuleTransactionParticipant>> participants, bool ownsConnection = true)
    {
        _connectionFactory = connectionFactory;
        _participants = participants;
        _ownsConnection = ownsConnection;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_active)
        {
            try { return await action(); }
            catch { _rollbackRequired = true; throw; }
        }

        var connection = _connectionFactory();
        await using var ownedConnection = _ownsConnection ? connection : null;
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var enlisted = new List<IModuleTransactionParticipant>();
        var committed = false;
        _active = true;
        _rollbackRequired = false;
        try
        {
            foreach (var participant in _participants())
            {
                await participant.EnlistAsync(connection, transaction, cancellationToken);
                enlisted.Add(participant);
            }

            var result = await action();
            if (_rollbackRequired)
                throw new InvalidOperationException("Una operación participante falló; se revierte la transacción completa.");
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            try
            {
                foreach (var participant in Enumerable.Reverse(enlisted))
                    await participant.ReleaseAsync(committed, CancellationToken.None);
            }
            finally { _active = false; _rollbackRequired = false; }
        }
    }
}
