using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: el Administrador confirma que la transferencia llego. Solo desde
// PendingVerification y solo si los pagos cubren el total.
public sealed class ConfirmInvoiceTransfer
{
    private readonly IInvoiceRepository _invoices;

    public ConfirmInvoiceTransfer(IInvoiceRepository invoices)
    {
        _invoices = invoices;
    }

    public async Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        invoice.ConfirmTransfer();

        await _invoices.SaveChangesAsync(cancellationToken);

        return invoice;
    }
}
