using System.Data;
using System.Data.Common;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Notifications.Infrastructure.Persistence;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class PruebasDeTransaccionEntreModulos
{
    [Fact]
    public async Task DosModulosCompartenConexionYUnaSolaConfirmacion()
    {
        var connection = new Conexion();
        var first = new Participante();
        var second = new Participante();
        var coordinator = new PostgresApplicationTransaction(() => connection, () => [first, second]);

        var result = await coordinator.ExecuteAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
        Assert.Same(first.Connection, second.Connection);
        Assert.Same(first.Transaction, second.Transaction);
        Assert.Equal(1, connection.Transaction.Commits);
        Assert.Equal(0, connection.Transaction.Rollbacks);
        Assert.True(first.Committed);
        Assert.True(second.Committed);
    }

    [Fact]
    public async Task FalloDeOperacionRevierteYLimpiaTodosLosParticipantes()
    {
        var connection = new Conexion();
        var first = new Participante();
        var second = new Participante();
        var coordinator = new PostgresApplicationTransaction(() => connection, () => [first, second]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync<int>(
            () => throw new InvalidOperationException("Fallo de escritura.")));

        Assert.Equal(0, connection.Transaction.Commits);
        Assert.Equal(1, connection.Transaction.Rollbacks);
        Assert.True(first.Released);
        Assert.True(second.Released);
        Assert.False(first.Committed);
        Assert.False(second.Committed);
    }

    [Fact]
    public async Task FalloAnidadoCapturadoImpideConfirmarLaOperacionExterior()
    {
        var connection = new Conexion();
        var coordinator = new PostgresApplicationTransaction(() => connection, () => []);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(async () =>
        {
            try { await coordinator.ExecuteAsync<int>(() => throw new InvalidOperationException("Fallo interno.")); }
            catch (InvalidOperationException) { }
            return 1;
        }));

        Assert.Equal(1, connection.Begins);
        Assert.Equal(0, connection.Transaction.Commits);
        Assert.Equal(1, connection.Transaction.Rollbacks);
    }

    [Fact]
    public async Task AnidamientoExitosoNoAbreOtraTransaccion()
    {
        var connection = new Conexion();
        var coordinator = new PostgresApplicationTransaction(() => connection, () => []);

        await coordinator.ExecuteAsync(() => coordinator.ExecuteAsync(() => Task.FromResult(1)));

        Assert.Equal(1, connection.Begins);
        Assert.Equal(1, connection.Transaction.Commits);
    }

    private sealed class Participante : IModuleTransactionParticipant
    {
        public DbConnection? Connection { get; private set; }
        public DbTransaction? Transaction { get; private set; }
        public bool Released { get; private set; }
        public bool Committed { get; private set; }
        public Task EnlistAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
        {
            Connection = connection;
            Transaction = transaction;
            return Task.CompletedTask;
        }
        public Task ReleaseAsync(bool committed, CancellationToken cancellationToken)
        {
            Released = true;
            Committed = committed;
            return Task.CompletedTask;
        }
    }

    private sealed class Conexion : DbConnection
    {
        public Transaccion Transaction { get; }
        public int Begins { get; private set; }
        public Conexion() => Transaction = new Transaccion(this);
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Prueba";
        public override string DataSource => "Prueba";
        public override string ServerVersion => "Prueba";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Close() { }
        public override void Open() { }
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            Begins++;
            return Transaction;
        }
    }

    private sealed class Transaccion(DbConnection connection) : DbTransaction
    {
        public int Commits { get; private set; }
        public int Rollbacks { get; private set; }
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => connection;
        public override void Commit() => Commits++;
        public override void Rollback() => Rollbacks++;
    }
}
