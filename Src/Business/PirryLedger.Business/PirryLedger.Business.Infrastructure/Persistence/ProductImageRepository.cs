using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.Persistence;

// El historial del conjunto de imagenes (ADR 015).
//
// Las escrituras reservan la revision y guardan el cambio en una sola
// transaccion, con el bloqueo del contador. Asi la revision N+1 nunca se
// confirma antes que la N y el cursor del cliente no se salta ningun cambio.
public sealed class ProductImageRepository : IProductImageRepository
{
    private readonly BusinessDbContext _context;

    public ProductImageRepository(BusinessDbContext context)
    {
        _context = context;
    }

    public Task<ProductImage?> FindLiveAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        _context.ProductImages.FirstOrDefaultAsync(
            image => image.ProductId == productId && image.RevisionRemoved == null,
            cancellationToken);

    public async Task<IReadOnlyList<ProductImage>> ListLiveAsync(
        CancellationToken cancellationToken = default) =>
        await _context.ProductImages
            .Where(image => image.RevisionRemoved == null)
            .OrderBy(image => image.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    // Las altas: las imagenes vigentes de productos activos que el cliente
    // todavia no tiene.
    //
    // Una imagen vuelve a entrar en el conjunto sin tocarse cuando el
    // producto se reactiva, y eso lo cuenta la revision del producto. Por eso
    // la consulta mira las dos revisiones.
    public async Task<IReadOnlyList<ProductImage>> ListUpsertsSinceAsync(
        long since,
        CancellationToken cancellationToken = default) =>
        await _context.ProductImages
            .Where(image => image.RevisionRemoved == null)
            .Join(
                _context.Products.Where(product => product.IsActive),
                image => image.ProductId,
                product => product.Id,
                (image, product) => new { image, product })
            .Where(row => row.image.RevisionAdded > since || row.product.ImageRevision > since)
            .Select(row => row.image)
            .ToListAsync(cancellationToken);

    // Las bajas: las claves que el cliente puede tener y que ya no estan.
    //
    // Son dos cosas: las imagenes que se quitaron del conjunto y las de los
    // productos que se desactivaron despues del cursor del cliente. Las
    // imagenes que se quitaron Y se volvieron a subir no cuentan como baja:
    // en ese caso la clave esta en las altas y el cliente la vuelve a
    // descargar en vez de borrarla.
    public async Task<IReadOnlyList<string>> ListRemovedKeysSinceAsync(
        long since,
        CancellationToken cancellationToken = default)
    {
        var retiradas = _context.ProductImages
            .Where(image => image.RevisionRemoved != null
                            && image.RevisionRemoved > since
                            && image.RevisionAdded <= since)
            .Select(image => image.ImageKey);

        var porDesactivar = _context.ProductImages
            .Where(image => image.RevisionRemoved == null)
            .Join(
                _context.Products.Where(product => !product.IsActive),
                image => image.ProductId,
                product => product.Id,
                (image, product) => new { image, product })
            .Where(row => row.product.ImageRevision > since)
            .Select(row => row.image.ImageKey);

        return await retiradas.Concat(porDesactivar).ToListAsync(cancellationToken);
    }

    public async Task<long> CurrentRevisionAsync(CancellationToken cancellationToken = default)
    {
        var counter = await _context.ImageRevisionCounters
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        return counter.Value;
    }

    public async Task<ImageSetChange> SetLiveImageAsync(
        ProductImage image,
        DateTime retiredAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var revision = await ReserveRevisionAsync(cancellationToken);

        var anterior = await _context.ProductImages
            .Where(item => item.ProductId == image.ProductId && item.RevisionRemoved == null)
            .FirstOrDefaultAsync(cancellationToken);

        string? retiredKey = null;

        if (anterior is not null)
        {
            anterior.Retire(revision, retiredAtUtc);
            retiredKey = anterior.ImageKey;
        }

        image.Enter(revision);
        await _context.ProductImages.AddAsync(image, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ImageSetChange(revision, retiredKey);
    }

    public async Task<ImageSetChange> RetireLiveImageAsync(
        Guid productId,
        DateTime retiredAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var revision = await ReserveRevisionAsync(cancellationToken);

        var vigente = await _context.ProductImages
            .Where(item => item.ProductId == productId && item.RevisionRemoved == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (vigente is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ImageSetChange(revision, null);
        }

        vigente.Retire(revision, retiredAtUtc);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ImageSetChange(revision, vigente.ImageKey);
    }

    // Reserva la siguiente revision. La reserva y el cambio se guardan en la
    // misma transaccion, asi que la revision coincide con el orden de
    // confirmacion.
    private async Task<long> ReserveRevisionAsync(CancellationToken cancellationToken) =>
        await ImageRevisionAllocator.ReserveAsync(_context, cancellationToken);
}
