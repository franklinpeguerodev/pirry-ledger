using System.Data;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Contracts.Reports;

namespace PirryLedger.Core.Reports.Infrastructure;

public sealed class ReportReadSession(IModuleDatabaseConnection database,
    IEnumerable<IModuleTransactionParticipant> participants) : IReportReadSession
{
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> query, CancellationToken cancellationToken = default)
    {
        var connection = database.Connection;
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var enlisted = new List<IModuleTransactionParticipant>();
        var committed = false;
        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SET TRANSACTION READ ONLY";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (var participant in participants)
            {
                await participant.EnlistAsync(connection, transaction, cancellationToken);
                enlisted.Add(participant);
            }
            var result = await query();
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
            foreach (var participant in Enumerable.Reverse(enlisted))
                await participant.ReleaseAsync(committed, CancellationToken.None);
        }
    }
}
