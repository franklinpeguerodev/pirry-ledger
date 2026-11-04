using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

public sealed class UpdateProduct
{
    private readonly IProductRepository _products;

    public UpdateProduct(IProductRepository products)
    {
        _products = products;
    }

    public async Task<Product> ExecuteAsync(
        Guid id,
        string name,
        string? description,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(id, cancellationToken)
            ?? throw new ProductNotFoundException();

        product.Update(name, description, price);
        await _products.SaveChangesAsync(cancellationToken);
        return product;
    }
}
