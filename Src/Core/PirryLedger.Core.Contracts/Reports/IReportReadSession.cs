namespace PirryLedger.Core.Contracts.Reports;

public interface IReportReadSession
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> query, CancellationToken cancellationToken = default);
}
