using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Business.Api;

public static class BusinessDayEndpoints
{
    public static void MapBusinessDays(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/billing/branches/{branchId:guid}/business-days/current",
            (Guid branchId, [FromServices] ManageBusinessDays days, CancellationToken ct) =>
                Run(async () => Results.Ok(Response(await days.CurrentAsync(branchId, ct)))))
            .WithAccess(Operation.GetBusinessDay);
        endpoints.MapPost("/api/billing/branches/{branchId:guid}/business-days/{id:guid}/close",
            (Guid branchId, Guid id, [FromServices] ManageBusinessDays days, CancellationToken ct) =>
                Run(async () => Results.Ok(Response(await days.CloseAsync(branchId, id, ct)))))
            .WithAccess(Operation.CloseBusinessDay);
        endpoints.MapPost("/api/billing/branches/{branchId:guid}/business-days/{id:guid}/reopen",
            (Guid branchId, Guid id, [FromServices] ManageBusinessDays days, CancellationToken ct) =>
                Run(async () => Results.Ok(Response(await days.ReopenAsync(branchId, id, ct)))))
            .WithAccess(Operation.ReopenBusinessDay);
    }

    private static object? Response(BusinessDay? day) => day is null ? null : new
    {
        day.Id, day.BranchId, day.BusinessDate, day.OpenedAtUtc, day.IssueDeadlineUtc,
        day.IsOpen, day.IsReopened, day.LastClosedAtUtc, day.LastClosedByUserId, day.Closing
    };

    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (UnfinishedInvoicesException error)
        {
            return Results.Json(new { message = error.Message, code = "UnfinishedInvoices", invoices = error.Invoices },
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (BusinessDayRuleViolationException error)
        {
            var status = error.Code switch
            {
                "BranchDenied" => StatusCodes.Status403Forbidden,
                "NotFound" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status409Conflict
            };
            return Results.Json(new { message = error.Message, code = error.Code }, statusCode: status);
        }
    }
}
