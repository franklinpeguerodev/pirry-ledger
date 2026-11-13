using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

public sealed class ListProducts
{
    private readonly IProductRepository _products;
    private readonly IProductImageRepository _images;
    private readonly IClassificationRepository? _classification;

    public ListProducts(IProductRepository products, IProductImageRepository images, IClassificationRepository? classification = null)
    {
        _products = products;
        _images = images;
        _classification = classification;
    }

    // El catalogo lleva la referencia a la imagen de cada producto: la clave
    // y la version. Nunca lleva una firma: las firmas se piden en la
    // sincronizacion y solo para lo que el cliente no tiene (ADR 015).
    public async Task<IReadOnlyList<ProductCatalogItem>> ExecuteAsync(
        CancellationToken cancellationToken = default, Guid? categoryId = null, Guid? subcategoryId = null, bool unclassified = false)
    {
        var products = await _products.ListAsync(cancellationToken);
        if (categoryId is Guid category && _classification is not null)
        {
            var subcategories = await _classification.ListSubcategoriesAsync(cancellationToken);
            var matching = subcategories.Where(item => item.CategoryId == category).Select(item => item.Id).ToHashSet();
            products = products.Where(product => product.SubcategoryId is Guid id && matching.Contains(id)).ToArray();
        }

        if (subcategoryId.HasValue)
            products = products.Where(product => product.SubcategoryId == subcategoryId).ToArray();

        if (unclassified)
            products = products.Where(product => product.SubcategoryId is null).ToArray();

        var images = await _images.ListLiveAsync(cancellationToken);

        var porProducto = images.ToDictionary(image => image.ProductId);

        var catalogo = new List<ProductCatalogItem>(products.Count);

        foreach (var product in products)
        {
            porProducto.TryGetValue(product.Id, out var image);
            catalogo.Add(ProductCatalogItem.From(product, image));
        }

        return catalogo;
    }
}
