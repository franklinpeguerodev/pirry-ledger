using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Reports;
using PirryLedger.Core.Reports.Application;

namespace PirryLedger.Core.Reports.Api;

public static class BusinessReportEndpointRegistration
{
    public static IEndpointRouteBuilder MapBusinessReports(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/reports/business/sales", (DateOnly from, DateOnly to,
            Guid[]? branchIds, string[]? productNames, SalesReportGrouping grouping, GetBusinessReports reports, CancellationToken ct) =>
            HandleAsync(() => reports.SalesAsync(from, to, branchIds ?? [], productNames ?? [], grouping, ct)))
            .WithAccess(Operation.ViewSalesReport);
        endpoints.MapGet("/api/reports/business/collections", (DateOnly from, DateOnly to,
            Guid[]? branchIds, GetBusinessReports reports, CancellationToken ct) =>
            HandleAsync(() => reports.MethodsAsync(from, to, branchIds ?? [], ct)))
            .WithAccess(Operation.ViewCollectionReport);
        endpoints.MapGet("/api/reports/business/products", (DateOnly from, DateOnly to,
            Guid[]? branchIds, string[]? productNames, GetBusinessReports reports, CancellationToken ct) =>
            HandleAsync(() => reports.ProductsAsync(from, to, branchIds ?? [], productNames ?? [], ct)))
            .WithAccess(Operation.ViewProductReport);
        endpoints.MapGet("/api/reports/business/exclusions", (DateOnly from, DateOnly to,
            Guid[]? branchIds, GetBusinessReports reports, CancellationToken ct) =>
            HandleAsync(() => reports.ExclusionsAsync(from, to, branchIds ?? [], ct)))
            .WithAccess(Operation.ViewExclusionReport);
        endpoints.MapGet("/api/reports/business/employees", (DateOnly from, DateOnly to,
            Guid[]? branchIds, string[]? productNames, GetBusinessReports reports, CancellationToken ct) =>
            HandleAsync(() => reports.EmployeesAsync(from, to, branchIds ?? [], productNames ?? [], ct)))
            .WithAccess(Operation.ViewEmployeeReport);
        endpoints.MapGet("/api/reports/business/debts/pending", (DateOnly from, DateOnly to,
            Guid[]? branchIds, int? offset, int? limit, GetBusinessReports reports, CancellationToken ct) =>
            HandleAsync(() => reports.DebtsAsync(from, to, branchIds ?? [], false, new(offset ?? 0, limit ?? 50), ct)))
            .WithAccess(Operation.ViewPendingDebtReport);
        endpoints.MapGet("/api/reports/business/debts/settled", (DateOnly from, DateOnly to,
            Guid[]? branchIds, int? offset, int? limit, GetBusinessReports reports, CancellationToken ct) =>
            HandleAsync(() => reports.DebtsAsync(from, to, branchIds ?? [], true, new(offset ?? 0, limit ?? 50), ct)))
            .WithAccess(Operation.ViewSettledDebtReport);
        endpoints.MapGet("/api/reports/business/closings", (DateOnly from, DateOnly to,
            Guid[]? branchIds, int? offset, int? limit, GetBusinessReports reports, CancellationToken ct) =>
            HandleAsync(() => reports.ClosingsAsync(from, to, branchIds ?? [], new(offset ?? 0, limit ?? 50), ct)))
            .WithAccess(Operation.ViewClosingReport);
        endpoints.MapGet("/api/reports/business/open-sales", (Guid[]? branchIds, GetBusinessReports reports, CancellationToken ct) =>
            HandleAsync(() => reports.OpenSalesAsync(branchIds ?? [], ct))).WithAccess(Operation.ViewOpenSalesReport);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync<T>(Func<Task<T>> query)
    {
        try
        {
            return Results.Ok(await query());
        }
        catch (ReportAccessDeniedException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: 403);
        }
        catch (BusinessReportsUnavailableException error)
        {
            return Results.NotFound(new { message = error.Message });
        }
        catch (ArgumentException error)
        {
            return Results.BadRequest(new { message = error.Message });
        }
    }
}
