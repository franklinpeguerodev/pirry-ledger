using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Application.UseCases;

public sealed class UpdateProduct
{
    private readonly IProductRepository _products;
    private readonly IClassificationRepository _classification;

    public UpdateProduct(IProductRepository products, IClassificationRepository classification)
    {
        _products = products;
        _classification = classification;
    }

    public async Task<Product> ExecuteAsync(
        Guid id,
        string name,
        string? description,
        decimal price,
        CancellationToken cancellationToken = default, Guid? subcategoryId = null, bool assignSubcategory = false)
    {
        if (assignSubcategory && subcategoryId is Guid selectedId &&
            await _classification.FindSubcategoryAsync(selectedId, cancellationToken) is null)
        {
            throw new InvoiceRuleViolationException("La subcategoría no existe.");
        }

        var product = await _products.GetByIdAsync(id, cancellationToken)
            ?? throw new ProductNotFoundException();

        product.Update(name, description, price);
        if (assignSubcategory)
            product.AssignSubcategory(subcategoryId);

        await _products.SaveChangesAsync(cancellationToken);
        return product;
    }
}
