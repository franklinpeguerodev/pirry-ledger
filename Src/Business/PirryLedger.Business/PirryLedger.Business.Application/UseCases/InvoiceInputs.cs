using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Application.UseCases;

// Entradas de los casos de uso del negocio. Son records porque el endpoint las
// arma desde el cuerpo HTTP y el caso de uso no necesita mutarlas.
public sealed record InvoiceItemInput(string Name, decimal UnitPrice, int Quantity);

public sealed record InvoicePaymentInput(
    PaymentMethod Method,
    decimal Amount,
    decimal? ReceivedAmount);
