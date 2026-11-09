using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// Quita la imagen de un producto (ADR 015).
//
// La fila sale del conjunto y, despues de confirmar, su objeto se borra del
// bucket. Al revés seria peligroso: si la confirmacion fallara, el objeto ya
// no estaria y la imagen vigente apuntaria a un archivo que no existe.
public sealed class RemoveProductImage
{
    private readonly IProductRepository _products;
    private readonly IProductImageRepository _images;
    private readonly IImageStore _store;
    private readonly IClock _clock;

    public RemoveProductImage(
        IProductRepository products,
        IProductImageRepository images,
        IImageStore store,
        IClock clock)
    {
        _products = products;
        _images = images;
        _store = store;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new ProductNotFoundException();

        var change = await _images.RetireLiveImageAsync(productId, _clock.UtcNow, cancellationToken);

        if (change.RetiredImageKey is null)
        {
            throw new ProductImageNotFoundException();
        }

        await _store.DeleteAsync(change.RetiredImageKey, cancellationToken);
    }
}
