using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.Persistence;

public sealed class ProductRepository : IProductRepository
{
    private readonly BusinessDbContext _context;

    public ProductRepository(BusinessDbContext context)
    {
        _context = context;
    }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Products.FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.Products
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);

    public Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
        _context.Products.AddAsync(product, cancellationToken).AsTask();

    // Activa o desactiva el producto y reserva la revision del conjunto de
    // imagenes en la misma transaccion (ADR 015).
    //
    // La foto de un producto desactivado se queda en el bucket: al reactivarlo
    // la vuelve a necesitar. Lo que cambia es si esa foto forma parte del
    // conjunto que el cliente descarga, y eso lo decide la revision.
    public async Task<long> SetActiveAsync(
        Product product,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var revision = await ImageRevisionAllocator.ReserveAsync(_context, cancellationToken);

        product.SetActive(isActive);
        product.MarkImageSetChanged(revision);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return revision;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
