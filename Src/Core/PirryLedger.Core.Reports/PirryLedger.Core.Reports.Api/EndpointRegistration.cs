using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Reports.Application;

namespace PirryLedger.Core.Reports.Api;

public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapReports(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/reports/permission-requests", async (DateOnly from, DateOnly to,
            Guid[]? branchIds, GetPermissionRequestReport report, CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await report.ExecuteAsync(from, to, branchIds ?? [], cancellationToken);
                return Results.Ok(result);
            }
            catch (ArgumentException error)
            {
                return Results.BadRequest(new { message = error.Message });
            }
            catch (ReportAccessDeniedException error)
            {
                return Results.Json(new { message = error.Message }, statusCode: 403);
            }
        }).WithAccess(Operation.ViewPermissionRequestReport);
        return endpoints;
    }
}
