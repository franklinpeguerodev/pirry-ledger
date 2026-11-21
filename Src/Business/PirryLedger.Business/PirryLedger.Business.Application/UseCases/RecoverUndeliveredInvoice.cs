using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

public sealed class RecoverUndeliveredInvoice
{
    private readonly INotificationPublisher? _notifications;
    private readonly IApplicationTransaction? _transaction;

    private readonly IInvoiceRepository _invoices;
    private readonly IBranchScopeAccessor _scope;
    private readonly IClock _clock;

    public RecoverUndeliveredInvoice(IInvoiceRepository invoices, IBranchScopeAccessor scope, IClock clock, INotificationPublisher? notifications = null, IApplicationTransaction? transaction = null)
    {
        _notifications = notifications;
        _transaction = transaction;
        _invoices = invoices;
        _scope = scope;
        _clock = clock;
    }

    public Task<Invoice> ExecuteAsync(Guid invoiceId, IReadOnlyCollection<InvoicePaymentInput> payments,
        CancellationToken cancellationToken = default) =>
        _transaction is null ? ExecuteCoreAsync(invoiceId, payments, cancellationToken)
            : _transaction.ExecuteAsync(() => ExecuteCoreAsync(invoiceId, payments, cancellationToken), cancellationToken);

    private async Task<Invoice> ExecuteCoreAsync(Guid invoiceId, IReadOnlyCollection<InvoicePaymentInput> payments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payments);
        if (_scope.IsGlobal || _scope.UserId is null || _scope.BranchId is null)
        {
            throw new InvoiceRecoveryDeniedException();
        }

        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);
        if (invoice.IssuedByUserId != _scope.UserId || invoice.BranchId != _scope.BranchId)
        {
            throw new InvoiceNotFoundException();
        }

        var registeredAt = _clock.UtcNow;
        invoice.RecoverUndelivered(payments.Select(payment =>
            InvoiceSalePaymentFactory.Create(payment, registeredAt, _scope.UserId.Value)).ToArray());
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
