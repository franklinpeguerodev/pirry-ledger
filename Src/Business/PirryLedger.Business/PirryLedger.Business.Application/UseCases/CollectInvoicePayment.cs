using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: cobrar la factura con uno o varios pagos. El metodo decide el
// tipo de pago; la maquina de estados decide si la factura queda Paid o
// PendingVerification.
public sealed class CollectInvoicePayment
{
    private readonly INotificationPublisher? _notifications;
    private readonly IApplicationTransaction? _transaction;

    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;

    public CollectInvoicePayment(IInvoiceRepository invoices, IClock clock, INotificationPublisher? notifications = null, IApplicationTransaction? transaction = null)
    {
        _notifications = notifications;
        _transaction = transaction;
        _invoices = invoices;
        _clock = clock;
    }

    public Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        IReadOnlyCollection<InvoicePaymentInput> payments,
        Guid registeredByUserId,
        CancellationToken cancellationToken = default) =>
        _transaction is null ? ExecuteCoreAsync(invoiceId, payments, registeredByUserId, cancellationToken)
            : _transaction.ExecuteAsync(() => ExecuteCoreAsync(invoiceId, payments, registeredByUserId, cancellationToken), cancellationToken);

    private async Task<Invoice> ExecuteCoreAsync(
        Guid invoiceId,
        IReadOnlyCollection<InvoicePaymentInput> payments,
        Guid registeredByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payments);

        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        var domainPayments = payments
            .Select(payment => InvoiceSalePaymentFactory.Create(payment, _clock.UtcNow, registeredByUserId))
            .ToList();

        invoice.Collect(domainPayments);

        await _invoices.SaveChangesAsync(cancellationToken);
        if (_notifications is not null)
            foreach (var payment in invoice.Payments.Where(payment => payment.Method == PaymentMethod.Transfer
                && payment.Status == PaymentStatus.Registered))
                await _notifications.PublishAsync(new NotificationEvent(NotificationEventKind.TransferRegistered,
                    payment.Id, $"Transferencia de {payment.Amount:F2} en factura {invoice.Id}. Sucursal: {invoice.BranchName}.",
                    BranchId: invoice.BranchId), cancellationToken);


        return invoice;
    }

}
