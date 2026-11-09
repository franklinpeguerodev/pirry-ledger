using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Helpers;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// Sube o reemplaza la imagen de un producto (ADR 015).
//
// El orden importa y es el que garantiza que una imagen vigente nunca apunte
// a un objeto que no existe:
//
// 1. se valida el archivo (tamano, formato, estructura);
// 2. se le quitan los metadatos;
// 3. se sube el objeto al bucket;
// 4. se guarda la fila en la base;
// 5. se borra el objeto anterior, si lo hubo.
//
// El objeto se sube antes de la transaccion y el anterior se borra despues de
// confirmar. Si la base falla, el objeto nuevo se borra en el momento: si
// tampoco se puede, queda como imagen residual, que se asume por ahora.
public sealed class SetProductImage
{
    private readonly IProductRepository _products;
    private readonly IProductImageRepository _images;
    private readonly IImageStore _store;
    private readonly IClock _clock;
    private readonly ProductImageOptions _options;

    public SetProductImage(
        IProductRepository products,
        IProductImageRepository images,
        IImageStore store,
        IClock clock,
        ProductImageOptions options)
    {
        _products = products;
        _images = images;
        _store = store;
        _clock = clock;
        _options = options;
    }

    public async Task<ProductImage> ExecuteAsync(
        Guid productId,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new ProductNotFoundException();

        if (!product.IsActive)
        {
            throw new ProductInactiveException();
        }

        if (content.Length > _options.MaxBytes)
        {
            throw new ProductImageTooLargeException(_options.MaxBytes);
        }

        var format = ImageFormatDetector.Detect(content)
            ?? throw new UnsupportedProductImageException();

        // El contenido que se guarda es el que ya no tiene metadatos: la
        // version es el sha256 de esos bytes.
        var limpio = ImageMetadataStripper.Strip(content, format);

        var version = ProductImage.ComputeVersion(limpio);
        var vigente = await _images.FindLiveAsync(productId, cancellationToken);

        // La misma imagen otra vez no cambia nada: no hay fila nueva, no hay
        // revision nueva y el bucket no se toca.
        if (vigente is not null && vigente.Version == version)
        {
            return vigente;
        }

        var image = ProductImage.Create(productId, limpio, format, _clock.UtcNow);

        await _store.PutAsync(image.ImageKey, limpio, image.ContentType, cancellationToken);

        ImageSetChange change;

        try
        {
            change = await _images.SetLiveImageAsync(image, _clock.UtcNow, cancellationToken);
        }
        catch
        {
            // La base no confirmo: el objeto que se subio no lo referencia
            // nadie. Se intenta borrar; si el bucket tampoco responde, queda
            // como imagen residual.
            await _store.DeleteAsync(image.ImageKey, cancellationToken);
            throw;
        }

        if (change.RetiredImageKey is not null)
        {
            await _store.DeleteAsync(change.RetiredImageKey, cancellationToken);
        }

        return image;
    }
}
