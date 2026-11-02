using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: marcar una factura de delivery como no entregada. Solo una
// factura de delivery puede hacerlo y el estado es terminal.
public sealed class MarkInvoiceAsNotDelivered
{
    private readonly IInvoiceRepository _invoices;

    public MarkInvoiceAsNotDelivered(IInvoiceRepository invoices)
    {
        _invoices = invoices;
    }

    public async Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        invoice.MarkAsNotDelivered();

        await _invoices.SaveChangesAsync(cancellationToken);

        return invoice;
    }
}
