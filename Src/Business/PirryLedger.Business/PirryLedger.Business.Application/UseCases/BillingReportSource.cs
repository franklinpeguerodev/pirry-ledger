using PirryLedger.Business.Application.Ports;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Reports;

namespace PirryLedger.Business.Application.UseCases;

public sealed class BillingReportSource(IBusinessReportRepository repository, IPendingInvoiceCancellations cancellations)
    : IBusinessReportSource
{
    private async Task<IReadOnlyList<Guid>> ExcludedAsync(BusinessReportFilter filter, bool open, CancellationToken ct) =>
        (await cancellations.FindAsync(await repository.InvoiceIdsAsync(filter, open, ct), ct)).ToArray();

    public async Task<IReadOnlyList<SalesReportRow>> SalesAsync(BusinessReportFilter filter, SalesReportGrouping grouping, CancellationToken ct)
    {
        var rows = await repository.DailySalesAsync(filter, await ExcludedAsync(filter, false, ct), ct);
        return rows.GroupBy(row => (row.BranchId, Period: Start(row.Period, grouping)))
            .Select(group => new SalesReportRow(group.Key.BranchId, group.Key.Period,
                group.Sum(row => row.InvoiceCount), group.Sum(row => row.Sold), SumMethods(group.Select(row => row.Methods))))
            .OrderBy(row => row.Period).ThenBy(row => row.BranchId).ToArray();
    }

    public async Task<IReadOnlyList<MethodReportRow>> MethodsAsync(BusinessReportFilter filter, CancellationToken ct) =>
        await repository.MethodsAsync(filter, await ExcludedAsync(filter, false, ct), ct);
    public async Task<ProductReportData> ProductsAsync(BusinessReportFilter filter, CancellationToken ct) =>
        await repository.ProductsAsync(filter, await ExcludedAsync(filter, false, ct), ct);
    public async Task<IReadOnlyList<ExclusionReportRow>> ExclusionsAsync(BusinessReportFilter filter, CancellationToken ct) =>
        await repository.ExclusionsAsync(filter, await ExcludedAsync(filter, false, ct), ct);
    public async Task<IReadOnlyList<EmployeeReportRow>> EmployeesAsync(BusinessReportFilter filter, CancellationToken ct) =>
        await repository.EmployeesAsync(filter, await ExcludedAsync(filter, false, ct), ct);
    public Task<DebtReportPage> DebtsAsync(BusinessReportFilter filter, bool settled, ReportPage page, CancellationToken ct) =>
        repository.DebtsAsync(filter, settled, page, ct);
    public Task<ClosingReportPage> ClosingsAsync(BusinessReportFilter filter, ReportPage page, CancellationToken ct) =>
        repository.ClosingsAsync(filter, page, ct);
    public async Task<IReadOnlyList<OpenSalesReportRow>> OpenSalesAsync(IReadOnlyList<Guid> branchIds, CancellationToken ct)
    {
        var filter = new BusinessReportFilter(DateOnly.MinValue, DateOnly.MaxValue, branchIds, []);
        return await repository.OpenSalesAsync(branchIds, await ExcludedAsync(filter, true, ct), ct);
    }

    private static DateOnly Start(DateOnly date, SalesReportGrouping grouping) => grouping switch
    {
        SalesReportGrouping.Day => date,
        SalesReportGrouping.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        SalesReportGrouping.Month => new(date.Year, date.Month, 1),
        _ => throw new ArgumentException("La agrupación de ventas es inválida.")
    };

    private static SaleMethods? SumMethods(IEnumerable<SaleMethods?> values)
    {
        var rows = values.ToArray();
        if (rows.Any(row => row is null)) return null;
        return new(rows.Sum(row => row!.Cash), rows.Sum(row => row!.ConfirmedTransfers),
            rows.Sum(row => row!.PendingTransfers), rows.Sum(row => row!.Debt));
    }
}
