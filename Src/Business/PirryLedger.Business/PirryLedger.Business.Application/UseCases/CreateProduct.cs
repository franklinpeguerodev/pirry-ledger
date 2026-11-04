using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

public sealed class CreateProduct
{
    private readonly IProductRepository _products;

    public CreateProduct(IProductRepository products)
    {
        _products = products;
    }

    public async Task<Product> ExecuteAsync(
        string name,
        string? description,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        var product = Product.Create(name, description, price);
        await _products.AddAsync(product, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
        return product;
    }
}
