using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Ports;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: editar los items de una factura emitida. El agregado valida el
// estado, conserva al menos un item, registra el historial y avisa.
public sealed class EditInvoiceItems
{
    private readonly IInvoiceRepository _invoices;
    private readonly IProductRepository? _products;
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

    public EditInvoiceItems(
        IInvoiceRepository invoices,
        IProductRepository products,
        IClock clock,
        IInvoiceEditedNotifier notifier)
        : this(invoices, clock, notifier)
    {
        _products = products;
    }

    public async Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        IReadOnlyCollection<ProductItemInput> items,
        Guid editedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (_products is null)
        {
            throw new InvalidOperationException("El catalogo de productos no esta configurado.");
        }

        var domainItems = new List<InvoiceItem>();
        foreach (var item in items)
        {
            var product = await _products.GetByIdAsync(item.ProductId, cancellationToken)
                ?? throw new ProductNotFoundException();

            if (!product.IsActive)
            {
                throw new ProductInactiveException();
            }

            domainItems.Add(InvoiceItem.Create(product, item.Quantity));
        }

        return await EditAsync(invoiceId, domainItems, editedByUserId, cancellationToken);
    }

    public async Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        IReadOnlyCollection<InvoiceItemInput> items,
        Guid editedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var domainItems = items
            .Select(item => InvoiceItem.Create(item.Name, item.UnitPrice, item.Quantity))
            .ToList();

        return await EditAsync(invoiceId, domainItems, editedByUserId, cancellationToken);
    }

    private async Task<Invoice> EditAsync(
        Guid invoiceId,
        IReadOnlyCollection<InvoiceItem> domainItems,
        Guid editedByUserId,
        CancellationToken cancellationToken)
    {
        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);
        invoice.EditItems(domainItems, _clock.UtcNow, editedByUserId, _notifier);

        await _invoices.SaveChangesAsync(cancellationToken);

        return invoice;
    }
}
