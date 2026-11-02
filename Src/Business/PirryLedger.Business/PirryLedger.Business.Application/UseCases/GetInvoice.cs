using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: consultar una factura con sus items y pagos.
public sealed class GetInvoice
{
    private readonly IInvoiceRepository _invoices;

    public GetInvoice(IInvoiceRepository invoices)
    {
        _invoices = invoices;
    }

    public Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        _invoices.GetRequiredAsync(invoiceId, cancellationToken);
}
