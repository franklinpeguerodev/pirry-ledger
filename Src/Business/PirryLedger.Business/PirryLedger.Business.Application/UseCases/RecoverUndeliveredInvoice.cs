using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

public sealed class RecoverUndeliveredInvoice
{
    private readonly IInvoiceRepository _invoices;
    private readonly IBranchScopeAccessor _scope;
    private readonly IClock _clock;

    public RecoverUndeliveredInvoice(IInvoiceRepository invoices, IBranchScopeAccessor scope, IClock clock)
    {
        _invoices = invoices;
        _scope = scope;
        _clock = clock;
    }

    public async Task<Invoice> ExecuteAsync(Guid invoiceId, IReadOnlyCollection<InvoicePaymentInput> payments,
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
        return invoice;
    }
}
