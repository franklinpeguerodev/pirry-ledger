using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Ports;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: editar los items de una factura emitida. El agregado valida el
// estado, conserva al menos un item, registra el historial y avisa.
public sealed class EditInvoiceItems
{
    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;
    private readonly IInvoiceEditedNotifier _notifier;

    public EditInvoiceItems(
        IInvoiceRepository invoices,
        IClock clock,
        IInvoiceEditedNotifier notifier)
    {
        _invoices = invoices;
        _clock = clock;
        _notifier = notifier;
    }

    public async Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        IReadOnlyCollection<InvoiceItemInput> items,
        Guid editedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        var domainItems = items
            .Select(item => InvoiceItem.Create(item.Name, item.UnitPrice, item.Quantity))
            .ToList();

        invoice.EditItems(domainItems, _clock.UtcNow, editedByUserId, _notifier);

        await _invoices.SaveChangesAsync(cancellationToken);

        return invoice;
    }
}
