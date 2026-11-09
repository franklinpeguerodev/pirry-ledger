using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

// La sincronizacion de las imagenes del catalogo (ADR 015).
//
// El cliente manda el cursor que el servidor le dio la ultima vez y el
// servidor responde con lo que cambio desde entonces: las altas (con su
// firma) y las bajas.
//
// El cliente no dice que imagenes tiene: dice "me diste hasta la revision N".
// El delta lo calcula el servidor con su propio historial, asi que el backend
// no depende de lo que el cliente afirme.
//
// El conjunto son las imagenes de los productos ACTIVOS. Desactivar un
// producto saca su foto del conjunto y reactivarlo la vuelve a meter.
public sealed class SyncProductImages
{
    private readonly IProductImageRepository _images;
    private readonly IImageStore _store;
    private readonly IClock _clock;
    private readonly ProductImageOptions _options;

    public SyncProductImages(
        IProductImageRepository images,
        IImageStore store,
        IClock clock,
        ProductImageOptions options)
    {
        _images = images;
        _store = store;
        _clock = clock;
        _options = options;
    }

    public async Task<ProductImageSyncResult> ExecuteAsync(
        long? since,
        CancellationToken cancellationToken = default)
    {
        var revision = await _images.CurrentRevisionAsync(cancellationToken);

        if (since is null)
        {
            // Sincronizacion completa: el cliente borra su cache y la
            // reconstruye con lo que llega.
            var todas = await _images.ListLiveActiveAsync(cancellationToken);

            return new ProductImageSyncResult(
                true,
                revision,
                await Firmar(todas, cancellationToken),
                []);
        }

        if (since.Value > revision)
        {
            throw new InvalidProductImageCursorException(since.Value, revision);
        }

        var altas = await _images.ListUpsertsSinceAsync(since.Value, cancellationToken);
        var bajas = await _images.ListRemovedKeysSinceAsync(since.Value, cancellationToken);
        var firmadas = await Firmar(altas, cancellationToken);

        // Una clave no puede estar en las dos listas. Si esta en las altas, el
        // cliente la vuelve a descargar en vez de borrarla: es lo que pasa
        // cuando un producto se desactiva y se reactiva.
        var clavesDeLasAltas = firmadas.Select(alta => alta.ImageKey).ToHashSet();
        var soloBajas = bajas.Where(clave => !clavesDeLasAltas.Contains(clave)).ToList();

        return new ProductImageSyncResult(false, revision, firmadas, soloBajas);
    }

    // Solo se firman las imagenes que el cliente no tiene. Cada firma es un
    // calculo local: no hay llamada de red.
    private async Task<IReadOnlyList<ProductImageUpsert>> Firmar(
        IReadOnlyList<ProductImage> images,
        CancellationToken cancellationToken)
    {
        var expiresAtUtc = _clock.UtcNow.AddMinutes(_options.SignatureMinutes);
        var firmadas = new List<ProductImageUpsert>(images.Count);

        foreach (var image in images)
        {
            var url = _store.CreateDownloadUrl(image.ImageKey, image.ContentType, expiresAtUtc);

            firmadas.Add(new ProductImageUpsert(
                image.ProductId,
                image.ImageKey,
                image.Version,
                image.ContentType,
                image.SizeBytes,
                url,
                expiresAtUtc));
        }

        return firmadas;
    }
}

// El resultado de la sincronizacion.
public sealed record ProductImageSyncResult(
    bool FullSync,
    long Revision,
    IReadOnlyList<ProductImageUpsert> Upserts,
    IReadOnlyList<string> Removed);

// Una imagen que el cliente tiene que descargar, con su firma.
public sealed record ProductImageUpsert(
    Guid ProductId,
    string ImageKey,
    string Version,
    string ContentType,
    int SizeBytes,
    string SignedUrl,
    DateTime ExpiresAtUtc);
