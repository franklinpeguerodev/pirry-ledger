using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Application.UseCases;

// Cobro y recuperación construyen pagos con las mismas reglas e identidad.
internal static class InvoiceSalePaymentFactory
{
    internal static InvoicePayment Create(InvoicePaymentInput input, DateTime registeredAtUtc, Guid userId) =>
        input.Method switch
        {
            PaymentMethod.Cash => InvoicePayment.Cash(input.Amount, input.ReceivedAmount, registeredAtUtc, userId),
            PaymentMethod.Transfer => InvoicePayment.Transfer(input.Amount, registeredAtUtc, userId),
            _ => throw new InvoiceRuleViolationException("El método de pago no es válido para un cobro."),
        };
}
