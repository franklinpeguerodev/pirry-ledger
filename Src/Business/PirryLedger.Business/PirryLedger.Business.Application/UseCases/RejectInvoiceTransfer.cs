using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: el Administrador rechaza una transferencia que no llego. La
// factura pasa a InDebt con el nombre de la persona.
public sealed class RejectInvoiceTransfer
{
    private readonly IInvoiceRepository _invoices;

    public RejectInvoiceTransfer(IInvoiceRepository invoices)
    {
        _invoices = invoices;
    }

    public async Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        string debtorName,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        invoice.RejectTransfer(debtorName);

        await _invoices.SaveChangesAsync(cancellationToken);

        return invoice;
    }
}
