using PirryLedger.Core.Contracts.Reports;

namespace PirryLedger.Business.Application.Ports;

public interface IBusinessReportRepository
{
    Task<IReadOnlyList<Guid>> InvoiceIdsAsync(BusinessReportFilter filter, bool open, CancellationToken ct);
    Task<IReadOnlyList<SalesReportRow>> DailySalesAsync(BusinessReportFilter filter, IReadOnlyList<Guid> excluded, CancellationToken ct);
    Task<IReadOnlyList<MethodReportRow>> MethodsAsync(BusinessReportFilter filter, IReadOnlyList<Guid> excluded, CancellationToken ct);
    Task<ProductReportData> ProductsAsync(BusinessReportFilter filter, IReadOnlyList<Guid> excluded, CancellationToken ct);
    Task<IReadOnlyList<ExclusionReportRow>> ExclusionsAsync(BusinessReportFilter filter, IReadOnlyList<Guid> excluded, CancellationToken ct);
    Task<IReadOnlyList<EmployeeReportRow>> EmployeesAsync(BusinessReportFilter filter, IReadOnlyList<Guid> excluded, CancellationToken ct);
    Task<DebtReportPage> DebtsAsync(BusinessReportFilter filter, bool settled, ReportPage page, CancellationToken ct);
    Task<ClosingReportPage> ClosingsAsync(BusinessReportFilter filter, ReportPage page, CancellationToken ct);
    Task<IReadOnlyList<OpenSalesReportRow>> OpenSalesAsync(IReadOnlyList<Guid> branchIds, IReadOnlyList<Guid> excluded, CancellationToken ct);
}
