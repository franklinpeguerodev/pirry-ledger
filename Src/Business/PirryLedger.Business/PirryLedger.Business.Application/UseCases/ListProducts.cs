using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

public sealed class ListProducts
{
    private readonly IProductRepository _products;

    public ListProducts(IProductRepository products)
    {
        _products = products;
    }

    public Task<IReadOnlyList<Product>> ExecuteAsync(
        CancellationToken cancellationToken = default) =>
        _products.ListAsync(cancellationToken);
}
