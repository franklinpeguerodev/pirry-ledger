using System.Data.Common;

namespace PirryLedger.Core.Contracts.Persistence;

public interface IApplicationTransaction
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
}

public interface IModuleDatabaseConnection : IAsyncDisposable
{
    DbConnection Connection { get; }
}

// Cada módulo incorpora exclusivamente su propio contexto.
public interface IModuleTransactionParticipant
{
    Task EnlistAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
    Task ReleaseAsync(bool committed, CancellationToken cancellationToken);
}
