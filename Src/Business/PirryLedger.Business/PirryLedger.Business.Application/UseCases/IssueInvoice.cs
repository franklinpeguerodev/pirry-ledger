using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: emitir la factura. El borrador vive solo en el cliente, asi que
// la operacion recibe los items y guarda la factura ya emitida. La regla de
// "al menos un item" la resuelve la maquina de estados.
public sealed class IssueInvoice
{
    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;

    public IssueInvoice(IInvoiceRepository invoices, IClock clock)
    {
        _invoices = invoices;
        _clock = clock;
    }

    public async Task<Invoice> ExecuteAsync(
        IReadOnlyCollection<InvoiceItemInput> items,
        bool isDelivery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var invoice = Invoice.Create();

        var domainItems = items
            .Select(item => InvoiceItem.Create(item.Name, item.UnitPrice, item.Quantity))
            .ToList();

        invoice.Issue(domainItems, isDelivery, _clock.UtcNow);

        await _invoices.AddAsync(invoice, cancellationToken);
        await _invoices.SaveChangesAsync(cancellationToken);

        return invoice;
    }
}
