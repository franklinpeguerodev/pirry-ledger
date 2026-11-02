using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: cobrar la factura con uno o varios pagos. El metodo decide el
// tipo de pago; la maquina de estados decide si la factura queda Paid o
// PendingVerification.
public sealed class CollectInvoicePayment
{
    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;

    public CollectInvoicePayment(IInvoiceRepository invoices, IClock clock)
    {
        _invoices = invoices;
        _clock = clock;
    }

    public async Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        IReadOnlyCollection<InvoicePaymentInput> payments,
        Guid registeredByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payments);

        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        var domainPayments = payments
            .Select(payment => BuildPayment(payment, registeredByUserId))
            .ToList();

        invoice.Collect(domainPayments);

        await _invoices.SaveChangesAsync(cancellationToken);

        return invoice;
    }

    private InvoicePayment BuildPayment(InvoicePaymentInput input, Guid registeredByUserId) =>
        input.Method switch
        {
            PaymentMethod.Cash => InvoicePayment.Cash(
                input.Amount,
                input.ReceivedAmount,
                _clock.UtcNow,
                registeredByUserId),
            PaymentMethod.Transfer => InvoicePayment.Transfer(
                input.Amount,
                _clock.UtcNow,
                registeredByUserId),
            _ => throw new InvoiceRuleViolationException(
                "El método de pago no es válido para un cobro."),
        };
}
