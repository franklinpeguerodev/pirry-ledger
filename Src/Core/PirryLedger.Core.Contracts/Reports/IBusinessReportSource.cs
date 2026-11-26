namespace PirryLedger.Core.Contracts.Reports;

public enum SalesReportGrouping { Day, Week, Month }
public sealed record BusinessReportFilter(DateOnly From, DateOnly To, IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<string> ProductNames);
public sealed record ReportPage(int Offset = 0, int Limit = 50);
public sealed record CountAmount(int Count, decimal Amount);
public sealed record SaleMethods(decimal Cash, decimal ConfirmedTransfers, decimal PendingTransfers, decimal Debt);
public sealed record SalesReportRow(Guid BranchId, DateOnly Period, int InvoiceCount, decimal Sold,
    SaleMethods? Methods);
public sealed record MethodReportRow(Guid BranchId, SaleMethods Methods);
public sealed record ProductReportRow(Guid BranchId, string Name, long Units, decimal Amount, int InvoiceCount);
public sealed record BranchInvoiceCount(Guid BranchId, int InvoiceCount);
public sealed record ProductReportData(IReadOnlyList<ProductReportRow> Rows, IReadOnlyList<BranchInvoiceCount> InvoiceCounts);
public sealed record ExclusionReportRow(Guid BranchId, int IssuedCount, CountAmount PendingCancellation,
    CountAmount Cancelled, CountAmount NotDelivered);
public sealed record EmployeeReportRow(Guid BranchId, Guid UserId, string HistoricalName, int IssuedCount,
    int SoldCount, decimal Sold,
    SaleMethods? Methods,
    CountAmount PendingCancellation, CountAmount Cancelled, CountAmount NotDelivered);
public sealed record DebtReportRow(Guid InvoiceId, Guid BranchId, string BranchName, DateOnly BusinessDate,
    DateTime IssuedAtUtc, string DebtorName, decimal InvoiceTotal, decimal Outstanding,
    bool HasPendingTransfer, DateTime? PaidAtUtc);
public sealed record DebtBranchTotals(Guid BranchId, int Count, decimal TotalInvoiced, decimal Outstanding);
public sealed record DebtReportPage(IReadOnlyList<DebtReportRow> Items, int TotalCount, decimal TotalInvoiced,
    decimal Outstanding, int Offset, int Limit,
    IReadOnlyList<DebtBranchTotals> BranchTotals);
public sealed record ClosingAmounts(decimal Sold, decimal Cash, decimal ConfirmedTransfers, decimal PendingTransfers,
    decimal Debt, decimal PendingCancellation, decimal Cancelled, decimal NotDelivered);
public sealed record ClosingReportRow(Guid BusinessDayId, Guid BranchId, DateOnly BusinessDate,
    DateTime ClosedAtUtc, Guid ClosedByUserId, bool IsReopened, ClosingAmounts Closing);
public sealed record ClosingBranchTotals(Guid BranchId, int Count, int ClosedCount, int ReopenedCount, ClosingAmounts ClosedTotals);
public sealed record ClosingReportPage(IReadOnlyList<ClosingReportRow> Items, int TotalCount,
    int ClosedCount, int ReopenedCount, ClosingAmounts ClosedTotals, int Offset, int Limit,
    IReadOnlyList<ClosingBranchTotals> BranchTotals);
public sealed record OpenSalesReportRow(Guid BusinessDayId, Guid BranchId, DateOnly BusinessDate, decimal Sold);

public interface IBusinessReportSource
{
    Task<IReadOnlyList<SalesReportRow>> SalesAsync(BusinessReportFilter filter, SalesReportGrouping grouping, CancellationToken ct);
    Task<IReadOnlyList<MethodReportRow>> MethodsAsync(BusinessReportFilter filter, CancellationToken ct);
    Task<ProductReportData> ProductsAsync(BusinessReportFilter filter, CancellationToken ct);
    Task<IReadOnlyList<ExclusionReportRow>> ExclusionsAsync(BusinessReportFilter filter, CancellationToken ct);
    Task<IReadOnlyList<EmployeeReportRow>> EmployeesAsync(BusinessReportFilter filter, CancellationToken ct);
    Task<DebtReportPage> DebtsAsync(BusinessReportFilter filter, bool settled, ReportPage page, CancellationToken ct);
    Task<ClosingReportPage> ClosingsAsync(BusinessReportFilter filter, ReportPage page, CancellationToken ct);
    Task<IReadOnlyList<OpenSalesReportRow>> OpenSalesAsync(IReadOnlyList<Guid> branchIds, CancellationToken ct);
}
