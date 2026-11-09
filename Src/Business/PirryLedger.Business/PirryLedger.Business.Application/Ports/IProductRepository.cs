using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.Ports;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    // Activa o desactiva el producto y reserva la revision del conjunto de
    // imagenes en la misma transaccion (ADR 015).
    //
    // Devuelve la revision reservada. Un producto que se desactiva saca su
    // imagen del conjunto y uno que se reactiva la vuelve a meter, y el
    // cliente tiene que enterarse de las dos cosas.
    Task<long> SetActiveAsync(
        Product product,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
