using System.Text.Json.Serialization;
using PirryLedger.Core.Contracts.Reports;

namespace PirryLedger.Core.Reports.Application;

public sealed record ReportBranch<TRow, TTotals>(Guid BranchId, string BranchName, IReadOnlyList<TRow> Rows, TTotals Totals);
public sealed record BranchReportTotals<T>(Guid BranchId, string BranchName, T Totals);
public sealed record SalesTotals(int InvoiceCount, decimal Sold,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SaleMethods? Methods);
public sealed record PeriodSalesTotals(DateOnly Period, SalesTotals Totals);
public sealed record SalesReportEntry(Guid BranchId, DateOnly Period, int InvoiceCount, decimal Sold,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SaleMethods? Methods);
public sealed record SalesReport(DateOnly From, DateOnly To, SalesReportGrouping Grouping,
    IReadOnlyList<ReportBranch<SalesReportEntry, SalesTotals>> Branches, IReadOnlyList<PeriodSalesTotals> Periods, SalesTotals Totals);
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
public sealed record EmployeeReportEntry(Guid BranchId, Guid UserId, string HistoricalName, int IssuedCount,
    int SoldCount, decimal Sold,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SaleMethods? Methods,
    CountAmount PendingCancellation, CountAmount Cancelled, CountAmount NotDelivered);
public sealed record EmployeeReport(DateOnly From, DateOnly To,
    IReadOnlyList<ReportBranch<EmployeeReportEntry, EmployeeTotals>> Branches,
    IReadOnlyList<CombinedEmployeeRow> Employees, EmployeeTotals Totals);
public sealed record DebtTotals(int InvoiceCount, decimal TotalInvoiced, decimal Outstanding);
public sealed record DebtReportPageResponse(IReadOnlyList<DebtReportRow> Items, int TotalCount, decimal TotalInvoiced,
    decimal Outstanding, int Offset, int Limit);
public sealed record DebtReport(DateOnly From, DateOnly To, IReadOnlyList<BranchReportTotals<DebtTotals>> Branches, DebtReportPageResponse Page);
public sealed record ClosingTotals(int Count, int ClosedCount, int ReopenedCount, ClosingAmounts ClosedTotals);
public sealed record ClosingReportEntry(Guid BusinessDayId, Guid BranchId, DateOnly BusinessDate,
    DateTime ClosedAtUtc, Guid ClosedByUserId, bool IsReopened, ClosingAmounts Closing)
{
    public string Status => IsReopened ? "Jornada reabierta" : "Jornada cerrada";
}
public sealed record ClosingReportPageResponse(IReadOnlyList<ClosingReportEntry> Items, int TotalCount,
    int ClosedCount, int ReopenedCount, ClosingAmounts ClosedTotals, int Offset, int Limit);
public sealed record ClosingReport(DateOnly From, DateOnly To, IReadOnlyList<BranchReportTotals<ClosingTotals>> Branches, ClosingReportPageResponse Page);
public sealed record OpenSalesReport(IReadOnlyList<ReportBranch<OpenSalesReportRow, decimal>> Branches, decimal Sold);
