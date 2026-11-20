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
    private readonly ManageBusinessDays? _days;

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

    public IssueInvoice(IInvoiceRepository invoices, IProductRepository products, IClock clock, ManageBusinessDays days)
        : this(invoices, products, clock) => _days = days;

    public async Task<Invoice> ExecuteAsync(
        IReadOnlyCollection<ProductItemInput> items,
        bool isDelivery,
        CancellationToken cancellationToken = default,
        Guid issuedByUserId = default,
        string issuedByUserName = "")
        => await ExecuteAsync(
            items,
            isDelivery,
            Guid.Empty,
            string.Empty,
            string.Empty,
            cancellationToken,
            issuedByUserId,
            issuedByUserName);

    public async Task<Invoice> ExecuteAsync(
        IReadOnlyCollection<ProductItemInput> items,
        bool isDelivery,
        Guid branchId,
        string branchName,
        string branchAddress,
        CancellationToken cancellationToken = default,
        Guid issuedByUserId = default,
        string issuedByUserName = "")
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

        return await IssueAsync(products, isDelivery, branchId, branchName, branchAddress, issuedByUserId, issuedByUserName, cancellationToken);
    }

    public async Task<Invoice> ExecuteAsync(
        IReadOnlyCollection<InvoiceItemInput> items,
        bool isDelivery,
        CancellationToken cancellationToken = default,
        Guid issuedByUserId = default,
        string issuedByUserName = "")
        => await ExecuteAsync(
            items,
            isDelivery,
            Guid.Empty,
            string.Empty,
            string.Empty,
            cancellationToken,
            issuedByUserId,
            issuedByUserName);

    public async Task<Invoice> ExecuteAsync(
        IReadOnlyCollection<InvoiceItemInput> items,
        bool isDelivery,
        Guid branchId,
        string branchName,
        string branchAddress,
        CancellationToken cancellationToken = default,
        Guid issuedByUserId = default,
        string issuedByUserName = "")
    {
        ArgumentNullException.ThrowIfNull(items);

        var domainItems = items
            .Select(item => InvoiceItem.Create(item.Name, item.UnitPrice, item.Quantity))
            .ToList();

        return await IssueAsync(domainItems, isDelivery, branchId, branchName, branchAddress, issuedByUserId, issuedByUserName, cancellationToken);
    }

    private async Task<Invoice> IssueAsync(
        IReadOnlyCollection<InvoiceItem> items,
        bool isDelivery,
        Guid branchId,
        string branchName,
        string branchAddress,
        Guid issuedByUserId,
        string issuedByUserName,
        CancellationToken cancellationToken)
    {
        async Task<Invoice> Persist(Guid? dayId)
        {
            var invoice = Invoice.Create();
            invoice.Issue(items, isDelivery, _clock.UtcNow, branchId, branchName, branchAddress,
                issuedByUserId, issuedByUserName);
            if (dayId is not null) invoice.AssignBusinessDay(dayId.Value);
            await _invoices.AddAsync(invoice, cancellationToken);
            await _invoices.SaveChangesAsync(cancellationToken);
            return invoice;
        }
        return _days is null ? await Persist(null)
            : await _days.IssueAsync(branchId, id => Persist(id), cancellationToken);
    }
}
