using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

public sealed class ListProducts
{
    private readonly IProductRepository _products;
    private readonly IProductImageRepository _images;

    public ListProducts(IProductRepository products, IProductImageRepository images)
    {
        _products = products;
        _images = images;
    }

    // El catalogo lleva la referencia a la imagen de cada producto: la clave
    // y la version. Nunca lleva una firma: las firmas se piden en la
    // sincronizacion y solo para lo que el cliente no tiene (ADR 015).
    public async Task<IReadOnlyList<ProductCatalogItem>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var products = await _products.ListAsync(cancellationToken);
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
