using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PirryLedger.Business.Api.Models;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Business.Api;

public static class DebtEndpoints
{
    public static void MapDebts(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/billing/debts", (Guid? branchId,
            [FromServices] ManageDebtPayments debts, CancellationToken cancellationToken) =>
            RunAsync(async () => Results.Ok(await debts.ListAsync(branchId, cancellationToken))))
            .WithAccess(Operation.ListDebts);

        endpoints.MapGet("/api/billing/debts/{id:guid}", (Guid id,
            [FromServices] ManageDebtPayments debts, CancellationToken cancellationToken) =>
            RunAsync(async () => Results.Ok(await debts.GetAsync(id, cancellationToken))))
            .WithAccess(Operation.GetDebt);

        endpoints.MapGet("/api/admin/debts/{id:guid}/payments", (Guid id,
            [FromServices] ManageDebtPayments debts, CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var invoice = await debts.HistoryAsync(id, cancellationToken);
                return Results.Ok(new
                {
                    invoice.Id,
                    PaidAtUtc = invoice.DebtPaidAtUtc,
                    Payments = invoice.Payments.OrderBy(payment => payment.RegisteredAtUtc)
                        .ThenBy(payment => payment.Id).Select(InvoicePaymentResponse.From).ToList()
                });
            })).WithAccess(Operation.GetDebtPaymentHistory);

        endpoints.MapPost("/api/admin/debts/{id:guid}/payments/{paymentId:guid}/confirm",
            (Guid id, Guid paymentId, [FromServices] ManageDebtPayments debts, CancellationToken cancellationToken) =>
            RunAsync(async () => Results.Ok(DebtSummary.From(await debts.ConfirmAsync(id, paymentId, cancellationToken)))))
            .WithAccess(Operation.ConfirmDebtTransfer);

        endpoints.MapPost("/api/admin/debts/{id:guid}/payments/{paymentId:guid}/reject",
            (Guid id, Guid paymentId, RejectDebtTransferBody body,
                [FromServices] ManageDebtPayments debts, CancellationToken cancellationToken) =>
            RunAsync(async () => Results.Ok(DebtSummary.From(await debts.RejectAsync(id, paymentId,
                body.Reason ?? string.Empty, cancellationToken)))))
            .WithAccess(Operation.RejectDebtTransfer);
    }

    internal static PaymentMethod ParseMethod(string? method)
    {
        if (!Enum.TryParse<PaymentMethod>(method, ignoreCase: true, out var parsed)
            || parsed is not (PaymentMethod.Cash or PaymentMethod.Transfer))
        {
            throw new InvoiceRuleViolationException("El método del abono debe ser Cash o Transfer.");
        }

        return parsed;
    }

    private static async Task<IResult> RunAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DebtAccessDeniedException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (InvoiceNotFoundException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvoiceRuleViolationException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
