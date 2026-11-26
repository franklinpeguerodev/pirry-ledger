using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Reports;

namespace PirryLedger.Core.Reports.Application;

public sealed class GetBusinessReports(IBranchDirectory directory, IBranchScopeAccessor scope,
    IReportReadSession readSession, IBusinessReportSource? source = null)
{
    private IBusinessReportSource Source => source ?? throw new BusinessReportsUnavailableException();

    public Task<SalesReport> SalesAsync(DateOnly from, DateOnly to, IReadOnlyList<Guid> branches,
        IReadOnlyList<string> products, SalesReportGrouping grouping, CancellationToken ct = default)
    {
        EnsureAdministrator();
        if (!Enum.IsDefined(grouping)) throw new ArgumentException("La agrupación de ventas es inválida.");
        return ReadAsync(from, to, branches, products, async (filter, selected) =>
        {
            var rows = await Source.SalesAsync(filter, grouping, ct);
            var methods = products.Count == 0;
            var branchRows = selected.Select(branch => new ReportBranch<SalesReportRow, SalesTotals>(branch.Id, branch.Name,
                rows.Where(row => row.BranchId == branch.Id).ToArray(), SumSales(rows.Where(row => row.BranchId == branch.Id), methods))).ToArray();
            var periods = rows.GroupBy(row => row.Period).OrderBy(group => group.Key)
                .Select(group => new PeriodSalesTotals(group.Key, SumSales(group, methods))).ToArray();
            return new SalesReport(from, to, grouping, branchRows, periods, SumSales(rows, methods));
        }, ct);
    }

    public Task<MethodReport> MethodsAsync(DateOnly from, DateOnly to, IReadOnlyList<Guid> branches, CancellationToken ct = default) =>
        ReadAsync(from, to, branches, [], async (filter, selected) =>
        {
            var rows = await Source.MethodsAsync(filter, ct);
            return new MethodReport(from, to, selected.Select(branch => new ReportBranch<MethodReportRow, SaleMethods>(
                branch.Id, branch.Name, rows.Where(row => row.BranchId == branch.Id).ToArray(),
                SumMethods(rows.Where(row => row.BranchId == branch.Id).Select(row => row.Methods)))).ToArray(),
                SumMethods(rows.Select(row => row.Methods)));
        }, ct);

    public Task<ProductReport> ProductsAsync(DateOnly from, DateOnly to, IReadOnlyList<Guid> branches,
        IReadOnlyList<string> products, CancellationToken ct = default) =>
        ReadAsync(from, to, branches, products, async (filter, selected) =>
        {
            var data = await Source.ProductsAsync(filter, ct);
            var rows = data.Rows;
            var groups = selected.Select(branch =>
            {
                var items = rows.Where(row => row.BranchId == branch.Id).OrderBy(row => row.Name, StringComparer.Ordinal).ToArray();
                return new ReportBranch<ProductReportRow, ProductTotals>(branch.Id, branch.Name, items,
                    new(data.InvoiceCounts.Where(row => row.BranchId == branch.Id).Sum(row => row.InvoiceCount),
                        items.Sum(row => row.Units), items.Sum(row => row.Amount)));
            }).ToArray();
            var combined = rows.GroupBy(row => row.Name, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new CombinedProductRow(group.Key, group.Sum(row => row.Units),
                    group.Sum(row => row.Amount), group.Sum(row => row.InvoiceCount))).ToArray();
            return new ProductReport(from, to, groups, combined,
                new(data.InvoiceCounts.Sum(row => row.InvoiceCount), rows.Sum(row => row.Units), rows.Sum(row => row.Amount)));
        }, ct);

    public Task<ExclusionReport> ExclusionsAsync(DateOnly from, DateOnly to, IReadOnlyList<Guid> branches, CancellationToken ct = default) =>
        ReadAsync(from, to, branches, [], async (filter, selected) =>
        {
            var rows = await Source.ExclusionsAsync(filter, ct);
            var totals = SumExclusions(rows);
            return new ExclusionReport(from, to, selected.Select(branch => new ReportBranch<ExclusionReportRow, ExclusionNumbers>(
                branch.Id, branch.Name, rows.Where(row => row.BranchId == branch.Id).ToArray(),
                SumExclusions(rows.Where(row => row.BranchId == branch.Id)))).ToArray(),
                new(totals, Percent(totals.PendingCancellation.Count, totals.IssuedCount), Percent(totals.Cancelled.Count, totals.IssuedCount)));
        }, ct);

    public Task<EmployeeReport> EmployeesAsync(DateOnly from, DateOnly to, IReadOnlyList<Guid> branches,
        IReadOnlyList<string> products, CancellationToken ct = default) =>
        ReadAsync(from, to, branches, products, async (filter, selected) =>
        {
            var rows = await Source.EmployeesAsync(filter, ct);
            var methods = products.Count == 0;
            var groups = selected.Select(branch => new ReportBranch<EmployeeReportRow, EmployeeTotals>(branch.Id, branch.Name,
                rows.Where(row => row.BranchId == branch.Id).OrderBy(row => row.HistoricalName, StringComparer.Ordinal)
                    .ThenBy(row => row.UserId).ToArray(),
                SumEmployees(rows.Where(row => row.BranchId == branch.Id), methods))).ToArray();
            var combined = rows.GroupBy(row => (row.UserId, row.HistoricalName))
                .OrderBy(group => group.Key.HistoricalName, StringComparer.Ordinal).ThenBy(group => group.Key.UserId)
                .Select(group => new CombinedEmployeeRow(group.Key.UserId, group.Key.HistoricalName, SumEmployees(group, methods))).ToArray();
            return new EmployeeReport(from, to, groups, combined, SumEmployees(rows, methods));
        }, ct);

    public Task<DebtReport> DebtsAsync(DateOnly from, DateOnly to, IReadOnlyList<Guid> branches,
        bool settled, ReportPage page, CancellationToken ct = default)
    {
        EnsureAdministrator();
        ValidatePage(page);
        return ReadAsync(from, to, branches, [], async (filter, selected) =>
        {
            var data = await Source.DebtsAsync(filter, settled, page, ct);
            var totals = selected.Select(branch => new BranchReportTotals<DebtTotals>(branch.Id, branch.Name,
                new(data.BranchTotals.Where(row => row.BranchId == branch.Id).Sum(row => row.Count),
                    data.BranchTotals.Where(row => row.BranchId == branch.Id).Sum(row => row.TotalInvoiced),
                    data.BranchTotals.Where(row => row.BranchId == branch.Id).Sum(row => row.Outstanding)))).ToArray();
            return new DebtReport(from, to, totals, data);
        }, ct);
    }

    public Task<ClosingReport> ClosingsAsync(DateOnly from, DateOnly to, IReadOnlyList<Guid> branches,
        ReportPage page, CancellationToken ct = default)
    {
        EnsureAdministrator();
        ValidatePage(page);
        return ReadAsync(from, to, branches, [], async (filter, selected) =>
        {
            var data = await Source.ClosingsAsync(filter, page, ct);
            var totals = selected.Select(branch =>
            {
                var saved = data.BranchTotals.SingleOrDefault(row => row.BranchId == branch.Id);
                return new BranchReportTotals<ClosingTotals>(branch.Id, branch.Name,
                    new(saved?.Count ?? 0, saved?.ClosedCount ?? 0, saved?.ReopenedCount ?? 0,
                        saved?.ClosedTotals ?? new(0, 0, 0, 0, 0, 0, 0, 0)));
            }).ToArray();
            return new ClosingReport(from, to, totals, data);
        }, ct);
    }

    public Task<OpenSalesReport> OpenSalesAsync(IReadOnlyList<Guid> branches, CancellationToken ct = default)
    {
        EnsureAdministrator();
        _ = Source;
        return readSession.ExecuteAsync(async () =>
        {
            var selected = await SelectBranchesAsync(branches, ct);
            var rows = await Source.OpenSalesAsync(selected.Select(branch => branch.Id).ToArray(), ct);
            return new OpenSalesReport(selected.Select(branch => new ReportBranch<OpenSalesReportRow, decimal>(branch.Id,
                branch.Name, rows.Where(row => row.BranchId == branch.Id).ToArray(),
                rows.Where(row => row.BranchId == branch.Id).Sum(row => row.Sold))).ToArray(), rows.Sum(row => row.Sold));
        }, ct);
    }

    private Task<T> ReadAsync<T>(DateOnly from, DateOnly to, IReadOnlyList<Guid> branches, IReadOnlyList<string> products,
        Func<BusinessReportFilter, IReadOnlyList<BranchScope>, Task<T>> query, CancellationToken ct)
    {
        EnsureAdministrator();
        if (from > to) throw new ArgumentException("El rango de jornadas es inválido.");
        if (products.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > 120))
            throw new ArgumentException("Los productos requieren nombres históricos de entre 1 y 120 caracteres.");
        _ = Source;
        return readSession.ExecuteAsync(async () =>
        {
            var selected = await SelectBranchesAsync(branches, ct);
            var filter = new BusinessReportFilter(from, to, selected.Select(branch => branch.Id).ToArray(),
                products.Distinct(StringComparer.Ordinal).ToArray());
            return await query(filter, selected);
        }, ct);
    }

    private async Task<IReadOnlyList<BranchScope>> SelectBranchesAsync(IReadOnlyList<Guid> requested, CancellationToken ct)
    {
        var available = await directory.ListAsync(ct);
        if (requested.Any(id => id == Guid.Empty || !available.Any(branch => branch.Id == id)))
            throw new ArgumentException("La selección contiene sucursales inválidas.");
        var all = requested.Count == 0 || available.Any(branch => branch.IsGlobal && requested.Contains(branch.Id));
        return available.Where(branch => !branch.IsGlobal && (all || requested.Contains(branch.Id)))
            .OrderBy(branch => branch.Name, StringComparer.Ordinal).ThenBy(branch => branch.Id).ToArray();
    }

    private void EnsureAdministrator()
    {
        if (!scope.IsGlobal || scope.UserId is null || scope.UserId == Guid.Empty) throw new ReportAccessDeniedException();
    }
    private static void ValidatePage(ReportPage page)
    {
        if (page.Offset < 0 || page.Limit is < 1 or > 100) throw new ArgumentException("La paginación requiere offset no negativo y limit entre 1 y 100.");
    }
    private static decimal Percent(int count, int total) => total == 0 ? 0m : count * 100m / total;
    private static SaleMethods SumMethods(IEnumerable<SaleMethods?> values)
    {
        var rows = values.Where(row => row is not null).ToArray();
        return new(rows.Sum(row => row!.Cash), rows.Sum(row => row!.ConfirmedTransfers),
            rows.Sum(row => row!.PendingTransfers), rows.Sum(row => row!.Debt));
    }
    private static SalesTotals SumSales(IEnumerable<SalesReportRow> values, bool methods)
    {
        var rows = values.ToArray();
        return new(rows.Sum(row => row.InvoiceCount), rows.Sum(row => row.Sold), methods ? SumMethods(rows.Select(row => row.Methods)) : null);
    }
    private static CountAmount SumAmounts(IEnumerable<CountAmount> values)
    {
        var rows = values.ToArray();
        return new(rows.Sum(row => row.Count), rows.Sum(row => row.Amount));
    }
    private static ExclusionNumbers SumExclusions(IEnumerable<ExclusionReportRow> values)
    {
        var rows = values.ToArray();
        return new(rows.Sum(row => row.IssuedCount), SumAmounts(rows.Select(row => row.PendingCancellation)),
            SumAmounts(rows.Select(row => row.Cancelled)), SumAmounts(rows.Select(row => row.NotDelivered)));
    }
    private static EmployeeTotals SumEmployees(IEnumerable<EmployeeReportRow> values, bool methods)
    {
        var rows = values.ToArray();
        return new(rows.Sum(row => row.IssuedCount), rows.Sum(row => row.SoldCount), rows.Sum(row => row.Sold),
            methods ? SumMethods(rows.Select(row => row.Methods)) : null,
            SumAmounts(rows.Select(row => row.PendingCancellation)), SumAmounts(rows.Select(row => row.Cancelled)),
            SumAmounts(rows.Select(row => row.NotDelivered)));
    }
}
