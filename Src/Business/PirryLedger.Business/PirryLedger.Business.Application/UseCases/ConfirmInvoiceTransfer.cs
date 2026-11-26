using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: el Administrador confirma que la transferencia llego. Solo desde
// PendingVerification y solo si los pagos cubren el total.
public sealed class ConfirmInvoiceTransfer
{
    private readonly INotificationPublisher? _notifications;
    private readonly IApplicationTransaction? _transaction;

    private readonly IInvoiceRepository _invoices;
    private readonly IClock? _clock;
    private readonly IBranchScopeAccessor? _scope;

    public ConfirmInvoiceTransfer(IInvoiceRepository invoices, INotificationPublisher? notifications = null,
        IApplicationTransaction? transaction = null, IClock? clock = null, IBranchScopeAccessor? scope = null)
    {
        _notifications = notifications;
        _transaction = transaction;
        _invoices = invoices;
        _clock = clock;
        _scope = scope;
    }

    public Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        _transaction is null ? ExecuteCoreAsync(invoiceId, cancellationToken)
            : _transaction.ExecuteAsync(() => ExecuteCoreAsync(invoiceId, cancellationToken), cancellationToken);

    private async Task<Invoice> ExecuteCoreAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        var resolved = invoice.Payments.Where(payment => payment.Method == PaymentMethod.Transfer
            && payment.Status == PaymentStatus.Registered).ToArray();
        invoice.ConfirmTransfer(_clock?.UtcNow, _scope?.UserId);

        await _invoices.SaveChangesAsync(cancellationToken);
        if (_notifications is not null)
            foreach (var payment in resolved)
                await _notifications.PublishAsync(new NotificationEvent(NotificationEventKind.TransferConfirmed, payment.Id,
                    $"Transferencia de factura {invoice.Id} confirmada.", invoice.IssuedByUserId, invoice.BranchId), cancellationToken);


        return invoice;
    }
}
