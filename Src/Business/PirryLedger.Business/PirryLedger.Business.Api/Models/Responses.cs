using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Api.Models;

// Respuestas HTTP del negocio. Se arman con records explicitos y no se
// serializa el agregado tal cual: asi el contrato HTTP no depende de las
// propiedades internas de la entidad ni de su coleccion escribible.
public sealed record InvoiceItemResponse(
    Guid Id,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal)
{
    public static InvoiceItemResponse From(InvoiceItem item) =>
        new(item.Id, item.Name, item.UnitPrice, item.Quantity, item.LineTotal);
}

public sealed record InvoicePaymentResponse(
    Guid Id,
    string Kind,
    string Method,
    string Status,
    decimal Amount,
    decimal? ReceivedAmount,
    decimal? ChangeAmount,
    DateTime RegisteredAtUtc,
    Guid RegisteredByUserId)
{
    public static InvoicePaymentResponse From(InvoicePayment payment) =>
        new(
            payment.Id,
            payment.Kind.ToString(),
            payment.Method.ToString(),
            payment.Status.ToString(),
            payment.Amount,
            payment.ReceivedAmount,
            payment.ChangeAmount,
            payment.RegisteredAtUtc,
            payment.RegisteredByUserId);
}

public sealed record InvoiceResponse(
    Guid Id,
    string State,
    bool IsDelivery,
    DateTime IssuedAtUtc,
    decimal Total,
    decimal PaidAmount,
    decimal Balance,
    string? DebtorName,
    IReadOnlyList<InvoiceItemResponse> Items,
    IReadOnlyList<InvoicePaymentResponse> Payments)
{
    public static InvoiceResponse From(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.State.ToString(),
            invoice.IsDelivery,
            invoice.IssuedAtUtc,
            invoice.Total,
            invoice.PaidAmount,
            invoice.Total - invoice.PaidAmount,
            invoice.DebtorName,
            invoice.Items.Select(InvoiceItemResponse.From).ToList(),
            invoice.Payments.Select(InvoicePaymentResponse.From).ToList());
}

public sealed record InvoiceEditResponse(
    Guid Id,
    Guid InvoiceId,
    DateTime EditedAtUtc,
    Guid EditedByUserId,
    int ItemCount)
{
    public static InvoiceEditResponse From(InvoiceEdit edit) =>
        new(edit.Id, edit.InvoiceId, edit.EditedAtUtc, edit.EditedByUserId, edit.ItemCount);
}
