using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PirryLedger.Business.Api.Models;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Domain.StateMachines;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Business.Api;

// RF-NEG-02 + RF-NEG-06: las rutas del negocio pertenecen al modulo del negocio
// (RD-01). Aqui solo se traduce HTTP a una llamada de caso de uso: ninguna regla
// vive en esta capa (RD-02). El acceso lo declara WithAccess y lo resuelve la
// tabla de RoleRequirements; el endpoint no compara roles.
public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapBusiness(this IEndpointRouteBuilder endpoints)
    {
        // RF-NEG-02: emitir una factura. El borrador vive solo en el cliente, asi
        // que se guarda ya emitida. Responde 201 con la factura creada.
        endpoints.MapPost("/api/billing/invoices", (
            IssueInvoiceBody body,
            IssueInvoice useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var invoice = await useCase.ExecuteAsync(
                    ToItems(body.Items),
                    body.IsDelivery,
                    cancellationToken);

                return Results.Json(
                    InvoiceResponse.From(invoice),
                    statusCode: StatusCodes.Status201Created);
            }))
        .WithAccess(Operation.IssueInvoice);

        // RF-NEG-02: cobrar la factura. El cajero que cobra sale de la sesion,
        // nunca del cuerpo de la peticion.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/collect", (
            Guid id,
            CollectPaymentBody body,
            CollectInvoicePayment useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                var invoice = await useCase.ExecuteAsync(
                    id,
                    ToPayments(body.Payments),
                    caller.Id,
                    cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.CollectInvoicePayment);

        // RF-NEG-02: dejar la factura como deuda, con o sin pago parcial.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/debt", (
            Guid id,
            LeaveAsDebtBody body,
            LeaveInvoiceAsDebt useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                var payments = body.Payments is null ? null : ToPayments(body.Payments);

                var invoice = await useCase.ExecuteAsync(
                    id,
                    body.DebtorName ?? string.Empty,
                    payments,
                    caller.Id,
                    cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.LeaveInvoiceAsDebt);

        // RF-NEG-02: editar los items de una factura emitida. Solo desde Issued;
        // el historial y el aviso al Administrator los resuelve el agregado.
        endpoints.MapPut("/api/billing/invoices/{id:guid}/items", (
            Guid id,
            EditItemsBody body,
            EditInvoiceItems useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                var invoice = await useCase.ExecuteAsync(
                    id,
                    ToItems(body.Items),
                    caller.Id,
                    cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.EditInvoiceItems);

        // RF-NEG-02: marcar una factura de delivery como no entregada.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/not-delivered", (
            Guid id,
            MarkInvoiceAsNotDelivered useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var invoice = await useCase.ExecuteAsync(id, cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.MarkInvoiceAsNotDelivered);

        // RF-NEG-06: anular. Desde Issued no pide permiso; desde Paid o InDebt
        // consume la concesion y, si no la hay, pide el permiso y rechaza con 403
        // (solicitud creada) o 409 (ya habia una pendiente).
        endpoints.MapPost("/api/billing/invoices/{id:guid}/cancel", (
            Guid id,
            CancelInvoice useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                var invoice = await useCase.ExecuteAsync(id, caller.Id, cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.CancelInvoice);

        // RF-NEG-02: el Administrator confirma que la transferencia llego.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/transfers/confirm", (
            Guid id,
            ConfirmInvoiceTransfer useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var invoice = await useCase.ExecuteAsync(id, cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.ConfirmInvoiceTransfer);

        // RF-NEG-02: el Administrator rechaza una transferencia que no llego.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/transfers/reject", (
            Guid id,
            RejectTransferBody body,
            RejectInvoiceTransfer useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var invoice = await useCase.ExecuteAsync(
                    id,
                    body.DebtorName ?? string.Empty,
                    cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.RejectInvoiceTransfer);

        // RF-NEG-02: el Administrator registra un abono de deuda.
        endpoints.MapPost("/api/billing/invoices/{id:guid}/payments", (
            Guid id,
            DebtPaymentBody body,
            RegisterDebtPayment useCase,
            Authenticate authenticate,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var caller = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                var invoice = await useCase.ExecuteAsync(
                    id,
                    body.Amount,
                    caller.Id,
                    cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.RegisterDebtPayment);

        // RF-NEG-02: consultar facturas. Cualquier sesion valida.
        endpoints.MapGet("/api/billing/invoices", (
            ListInvoices useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var invoices = await useCase.ExecuteAsync(cancellationToken);

                return Results.Ok(invoices.Select(InvoiceResponse.From).ToList());
            }))
        .WithAccess(Operation.ListInvoices);

        endpoints.MapGet("/api/billing/invoices/{id:guid}", (
            Guid id,
            GetInvoice useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var invoice = await useCase.ExecuteAsync(id, cancellationToken);

                return Results.Ok(InvoiceResponse.From(invoice));
            }))
        .WithAccess(Operation.GetInvoice);

        // RF-NEG-02: el historial de edicion es solo del Administrator.
        endpoints.MapGet("/api/billing/invoices/{id:guid}/edits", (
            Guid id,
            ListInvoiceEdits useCase,
            CancellationToken cancellationToken) =>
            RunAsync(async () =>
            {
                var edits = await useCase.ExecuteAsync(id, cancellationToken);

                return Results.Ok(edits.Select(InvoiceEditResponse.From).ToList());
            }))
        .WithAccess(Operation.ListInvoiceEdits);

        return endpoints;
    }

    // Traduce las excepciones conocidas del negocio a un rechazo controlado
    // (RD-07, RD-08). Cualquier otra sube a la frontera de error del API, que la
    // registra y responde un mensaje generico sin trazas.
    private static async Task<IResult> RunAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InvoiceNotFoundException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (CancellationPermissionRequiredException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: error.AlreadyPending
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status403Forbidden);
        }
        catch (InvoiceRuleViolationException error)
        {
            return BadRequest(error.Message);
        }
        catch (InvalidInvoiceTransitionException error)
        {
            return BadRequest(error.Message);
        }
        catch (InvoiceConditionNotMetException error)
        {
            return BadRequest(error.Message);
        }
    }

    private static IResult BadRequest(string message) =>
        Results.Json(
            new ErrorResponse(message),
            statusCode: StatusCodes.Status400BadRequest);

    private static IReadOnlyCollection<InvoiceItemInput> ToItems(List<InvoiceItemBody>? items) =>
        items?
            .Select(item => new InvoiceItemInput(
                item.Name ?? string.Empty,
                item.UnitPrice,
                item.Quantity))
            .ToList()
        ?? new List<InvoiceItemInput>();

    private static IReadOnlyCollection<InvoicePaymentInput> ToPayments(List<PaymentBody>? payments) =>
        payments?
            .Select(payment => new InvoicePaymentInput(
                ParseMethod(payment.Method),
                payment.Amount,
                payment.ReceivedAmount))
            .ToList()
        ?? new List<InvoicePaymentInput>();

    // PaymentMethod tiene un valor (None) que no es un medio de pago valido para
    // cobrar. Se acepta solo Cash o Transfer; cualquier otra cosa es un 400 con
    // un mensaje en espanol, no un 500 del binding.
    private static PaymentMethod ParseMethod(string? value)
    {
        if (Enum.TryParse<PaymentMethod>(value, ignoreCase: true, out var method)
            && method is PaymentMethod.Cash or PaymentMethod.Transfer)
        {
            return method;
        }

        throw new InvoiceRuleViolationException(
            "El método de pago no es válido; usa Cash o Transfer.");
    }
}
