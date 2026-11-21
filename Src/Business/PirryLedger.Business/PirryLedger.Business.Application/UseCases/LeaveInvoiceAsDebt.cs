using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: dejar la factura como deuda con el nombre del deudor y, si hubo
// un pago parcial, registrarlo en la misma operacion.
public sealed class LeaveInvoiceAsDebt
{
    private readonly INotificationPublisher? _notifications;
    private readonly IApplicationTransaction? _transaction;

    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;

    public LeaveInvoiceAsDebt(IInvoiceRepository invoices, IClock clock, INotificationPublisher? notifications = null, IApplicationTransaction? transaction = null)
    {
        _notifications = notifications;
        _transaction = transaction;
        _invoices = invoices;
        _clock = clock;
    }

    public Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        string debtorName,
        IReadOnlyCollection<InvoicePaymentInput>? payments,
        Guid registeredByUserId,
        CancellationToken cancellationToken = default) =>
        _transaction is null ? ExecuteCoreAsync(invoiceId, debtorName, payments, registeredByUserId, cancellationToken)
            : _transaction.ExecuteAsync(() => ExecuteCoreAsync(invoiceId, debtorName, payments, registeredByUserId, cancellationToken), cancellationToken);

    private async Task<Invoice> ExecuteCoreAsync(
        Guid invoiceId,
        string debtorName,
        IReadOnlyCollection<InvoicePaymentInput>? payments,
        Guid registeredByUserId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        var domainPayments = payments?.Select(payment => BuildPayment(payment, registeredByUserId)).ToList();

        invoice.LeaveAsDebt(debtorName, domainPayments);

        await _invoices.SaveChangesAsync(cancellationToken);
        if (_notifications is not null)
            foreach (var payment in invoice.Payments.Where(payment => payment.Method == PaymentMethod.Transfer
                && payment.Status == PaymentStatus.Registered))
                await _notifications.PublishAsync(new NotificationEvent(NotificationEventKind.TransferRegistered,
                    payment.Id, $"Transferencia de {payment.Amount:F2} en factura {invoice.Id}. Sucursal: {invoice.BranchName}.",
                    BranchId: invoice.BranchId), cancellationToken);
        if (_notifications is not null)
            await _notifications.PublishAsync(new NotificationEvent(NotificationEventKind.DebtCreated,
                invoice.Id, $"Factura {invoice.Id} fiada a {invoice.DebtorName}. Sucursal: {invoice.BranchName}.",
                BranchId: invoice.BranchId), cancellationToken);


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
                "El método de pago no es válido para un pago parcial."),
        };
}
