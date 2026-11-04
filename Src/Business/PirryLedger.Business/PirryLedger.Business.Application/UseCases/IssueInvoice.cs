using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: emitir la factura. El borrador vive solo en el cliente, asi que
// la operacion recibe los items y guarda la factura ya emitida. La regla de
// "al menos un item" la resuelve la maquina de estados.
public sealed class IssueInvoice
{
    private readonly IInvoiceRepository _invoices;
    private readonly IProductRepository? _products;
    private readonly IClock _clock;

    public IssueInvoice(IInvoiceRepository invoices, IClock clock)
    {
        _invoices = invoices;
        _clock = clock;
    }

    public IssueInvoice(IInvoiceRepository invoices, IProductRepository products, IClock clock)
        : this(invoices, clock)
    {
        _products = products;
    }

    public async Task<Invoice> ExecuteAsync(
        IReadOnlyCollection<ProductItemInput> items,
        bool isDelivery,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            items,
            isDelivery,
            Guid.Empty,
            string.Empty,
            string.Empty,
            cancellationToken);

    public async Task<Invoice> ExecuteAsync(
        IReadOnlyCollection<ProductItemInput> items,
        bool isDelivery,
        Guid branchId,
        string branchName,
        string branchAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (_products is null)
        {
            throw new InvalidOperationException("El catalogo de productos no esta configurado.");
        }

        var products = new List<InvoiceItem>();

        foreach (var item in items)
        {
            var product = await _products.GetByIdAsync(item.ProductId, cancellationToken)
                ?? throw new ProductNotFoundException();

            if (!product.IsActive)
            {
                throw new ProductInactiveException();
            }

            products.Add(InvoiceItem.Create(product, item.Quantity));
        }

        return await IssueAsync(products, isDelivery, branchId, branchName, branchAddress, cancellationToken);
    }

    public async Task<Invoice> ExecuteAsync(
        IReadOnlyCollection<InvoiceItemInput> items,
        bool isDelivery,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            items,
            isDelivery,
            Guid.Empty,
            string.Empty,
            string.Empty,
            cancellationToken);

    public async Task<Invoice> ExecuteAsync(
        IReadOnlyCollection<InvoiceItemInput> items,
        bool isDelivery,
        Guid branchId,
        string branchName,
        string branchAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var domainItems = items
            .Select(item => InvoiceItem.Create(item.Name, item.UnitPrice, item.Quantity))
            .ToList();

        return await IssueAsync(domainItems, isDelivery, branchId, branchName, branchAddress, cancellationToken);
    }

    private async Task<Invoice> IssueAsync(
        IReadOnlyCollection<InvoiceItem> items,
        bool isDelivery,
        Guid branchId,
        string branchName,
        string branchAddress,
        CancellationToken cancellationToken)
    {
        var invoice = Invoice.Create();
        invoice.Issue(items, isDelivery, _clock.UtcNow, branchId, branchName, branchAddress);
        await _invoices.AddAsync(invoice, cancellationToken);
        await _invoices.SaveChangesAsync(cancellationToken);

        return invoice;
    }
}
