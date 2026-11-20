using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Persistence;

namespace PirryLedger.Core.Permissions.Infrastructure.Persistence;

public sealed partial class PermissionsDbContext : IModuleTransactionParticipant
{
    public async Task EnlistAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(Database.GetDbConnection(), connection) || Database.CurrentTransaction is not null)
            throw new InvalidOperationException("El contexto debe incorporarse antes de iniciar operaciones transaccionales.");
        await Database.UseTransactionAsync(transaction, cancellationToken);
    }

    public async Task ReleaseAsync(bool committed, CancellationToken cancellationToken)
    {
        await Database.UseTransactionAsync(null, cancellationToken);
        if (!committed) ChangeTracker.Clear();
    }
}
