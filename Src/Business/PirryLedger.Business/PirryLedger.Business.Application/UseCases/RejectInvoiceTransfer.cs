using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: el Administrador rechaza una transferencia que no llego. La
// factura pasa a InDebt con el nombre de la persona.
public sealed class RejectInvoiceTransfer
{
    private readonly INotificationPublisher? _notifications;
    private readonly IApplicationTransaction? _transaction;

    private readonly IInvoiceRepository _invoices;
    private readonly IClock? _clock;
    private readonly IBranchScopeAccessor? _scope;

    public RejectInvoiceTransfer(IInvoiceRepository invoices, INotificationPublisher? notifications = null,
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
        string debtorName,
        CancellationToken cancellationToken = default, string? reason = null) =>
        _transaction is null ? ExecuteCoreAsync(invoiceId, debtorName, cancellationToken, reason)
            : _transaction.ExecuteAsync(() => ExecuteCoreAsync(invoiceId, debtorName, cancellationToken, reason), cancellationToken);

    private async Task<Invoice> ExecuteCoreAsync(
        Guid invoiceId,
        string debtorName,
        CancellationToken cancellationToken = default, string? reason = null)
    {
        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        if (string.IsNullOrWhiteSpace(reason))
            throw new PirryLedger.Business.Domain.Rules.InvoiceRuleViolationException("El motivo del rechazo es obligatorio.");
        var resolved = invoice.Payments.Where(payment => payment.Method == PaymentMethod.Transfer
            && payment.Status == PaymentStatus.Registered).ToArray();
        invoice.RejectTransfer(debtorName, _clock?.UtcNow, _scope?.UserId, reason);

        await _invoices.SaveChangesAsync(cancellationToken);
        if (_notifications is not null)
            foreach (var payment in resolved)
                await _notifications.PublishAsync(new NotificationEvent(NotificationEventKind.TransferRejected, payment.Id,
                    $"Transferencia de factura {invoice.Id} rechazada. Motivo: {reason}.", invoice.IssuedByUserId, invoice.BranchId), cancellationToken);


        return invoice;
    }
}
