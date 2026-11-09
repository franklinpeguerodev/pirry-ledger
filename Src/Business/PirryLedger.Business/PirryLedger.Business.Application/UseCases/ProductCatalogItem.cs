using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

// Un producto del catalogo con la referencia a su imagen.
//
// La imagen no es un atributo del producto: es una fila con su propio ciclo
// de vida (ADR 015). Lo que se lleva el cliente es la clave y la version,
// nunca una firma: las firmas se piden en la sincronizacion y solo para lo
// que el cliente no tiene.
public sealed record ProductCatalogItem(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    bool IsActive,
    string? ImageKey,
    string? ImageVersion)
{
    public static ProductCatalogItem From(Product product, ProductImage? image) =>
        new(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.IsActive,
            image?.ImageKey,
            image?.Version);
}
