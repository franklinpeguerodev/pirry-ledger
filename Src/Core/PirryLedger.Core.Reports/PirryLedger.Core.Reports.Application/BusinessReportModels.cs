using System.Text.Json.Serialization;
using PirryLedger.Core.Contracts.Reports;

namespace PirryLedger.Core.Reports.Application;

public sealed record ReportBranch<TRow, TTotals>(Guid BranchId, string BranchName, IReadOnlyList<TRow> Rows, TTotals Totals);
public sealed record BranchReportTotals<T>(Guid BranchId, string BranchName, T Totals);
public sealed record SalesTotals(int InvoiceCount, decimal Sold,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SaleMethods? Methods);
public sealed record PeriodSalesTotals(DateOnly Period, SalesTotals Totals);
public sealed record SalesReport(DateOnly From, DateOnly To, SalesReportGrouping Grouping,
    IReadOnlyList<ReportBranch<SalesReportRow, SalesTotals>> Branches, IReadOnlyList<PeriodSalesTotals> Periods, SalesTotals Totals);
public sealed record MethodReport(DateOnly From, DateOnly To, IReadOnlyList<ReportBranch<MethodReportRow, SaleMethods>> Branches, SaleMethods Totals);
public sealed record ProductTotals(int InvoiceCount, long Units, decimal Amount);
public sealed record CombinedProductRow(string Name, long Units, decimal Amount, int InvoiceCount);
public sealed record ProductReport(DateOnly From, DateOnly To, IReadOnlyList<ReportBranch<ProductReportRow, ProductTotals>> Branches,
    IReadOnlyList<CombinedProductRow> Products, ProductTotals Totals);
public sealed record ExclusionNumbers(int IssuedCount, CountAmount PendingCancellation, CountAmount Cancelled, CountAmount NotDelivered);
public sealed record ExclusionTotals(ExclusionNumbers Values, decimal PendingCancellationPercentage, decimal CancelledPercentage);
public sealed record ExclusionReport(DateOnly From, DateOnly To,
    IReadOnlyList<ReportBranch<ExclusionReportRow, ExclusionNumbers>> Branches, ExclusionTotals Totals);
public sealed record EmployeeTotals(int IssuedCount, int SoldCount, decimal Sold,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SaleMethods? Methods,
    CountAmount PendingCancellation, CountAmount Cancelled, CountAmount NotDelivered);
public sealed record CombinedEmployeeRow(Guid UserId, string HistoricalName, EmployeeTotals Totals);
public sealed record EmployeeReport(DateOnly From, DateOnly To,
    IReadOnlyList<ReportBranch<EmployeeReportRow, EmployeeTotals>> Branches,
    IReadOnlyList<CombinedEmployeeRow> Employees, EmployeeTotals Totals);
public sealed record DebtTotals(int InvoiceCount, decimal TotalInvoiced, decimal Outstanding);
public sealed record DebtReport(DateOnly From, DateOnly To, IReadOnlyList<BranchReportTotals<DebtTotals>> Branches, DebtReportPage Page);
public sealed record ClosingTotals(int Count, int ClosedCount, int ReopenedCount, ClosingAmounts ClosedTotals);
public sealed record ClosingReport(DateOnly From, DateOnly To, IReadOnlyList<BranchReportTotals<ClosingTotals>> Branches, ClosingReportPage Page);
public sealed record OpenSalesReport(IReadOnlyList<ReportBranch<OpenSalesReportRow, decimal>> Branches, decimal Sold);
