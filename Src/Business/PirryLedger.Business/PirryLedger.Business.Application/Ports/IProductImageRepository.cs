using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.Ports;

// El historial del conjunto de imagenes (ADR 015).
//
// Cada cambio del conjunto inserta una fila y deja la anterior con la
// revision en la que salio. El historial es el propio dato: no hace falta una
// tabla de auditoria aparte.
//
// El repositorio reserva la revision y guarda el cambio en una sola
// transaccion. El bloqueo del contador serializa las escrituras, asi que la
// revision N+1 nunca se confirma antes que la N y el cursor del cliente no se
// salta ningun cambio.
public interface IProductImageRepository
{
    // La imagen vigente del producto, si la tiene.
    Task<ProductImage?> FindLiveAsync(Guid productId, CancellationToken cancellationToken = default);

    // Todas las imagenes vigentes, de productos activos o inactivos.
    Task<IReadOnlyList<ProductImage>> ListLiveAsync(CancellationToken cancellationToken = default);

    // Las altas: las imagenes vigentes de productos activos que el cliente
    // todavia no tiene. Incluye las que vuelven porque el producto se
    // reactivo.
    Task<IReadOnlyList<ProductImage>> ListUpsertsSinceAsync(long since, CancellationToken cancellationToken = default);

    // Las bajas: las claves que el cliente puede tener y que ya no estan. Son
    // las imagenes que se quitaron y las de los productos que se desactivaron.
    Task<IReadOnlyList<string>> ListRemovedKeysSinceAsync(long since, CancellationToken cancellationToken = default);

    // La revision actual del conjunto. Es el cursor que el cliente guarda.
    Task<long> CurrentRevisionAsync(CancellationToken cancellationToken = default);

    // Sustituye la imagen vigente del producto por otra, con una sola revision
    // nueva. Devuelve la revision y la clave de la imagen que salio, si habia
    // alguna, para poder borrar su objeto en el bucket despues.
    Task<ImageSetChange> SetLiveImageAsync(
        ProductImage image,
        DateTime retiredAtUtc,
        CancellationToken cancellationToken = default);

    // Saca la imagen vigente del producto del conjunto.
    Task<ImageSetChange> RetireLiveImageAsync(
        Guid productId,
        DateTime retiredAtUtc,
        CancellationToken cancellationToken = default);
}
