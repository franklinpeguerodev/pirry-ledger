using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Application.UseCases;

public sealed class CreateProduct
{
    private readonly IProductRepository _products;
    private readonly IClassificationRepository _classification;

    public CreateProduct(IProductRepository products, IClassificationRepository classification)
    {
        _products = products;
        _classification = classification;
    }

    public async Task<Product> ExecuteAsync(
        string name,
        string? description,
        decimal price,
        CancellationToken cancellationToken = default, Guid? subcategoryId = null)
    {
        if (subcategoryId is Guid selectedId &&
            await _classification.FindSubcategoryAsync(selectedId, cancellationToken) is null)
        {
            throw new InvoiceRuleViolationException("La subcategoría no existe.");
        }

        var product = Product.Create(name, description, price);
        product.AssignSubcategory(subcategoryId);
        await _products.AddAsync(product, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
        return product;
    }
}
