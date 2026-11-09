using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

public sealed class SetProductActive
{
    private readonly IProductRepository _products;

    public SetProductActive(IProductRepository products)
    {
        _products = products;
    }

    public async Task<Product> ExecuteAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(id, cancellationToken)
            ?? throw new ProductNotFoundException();

        // La revision del conjunto de imagenes se reserva en la misma
        // transaccion que el cambio de estado: si no, el cliente podria
        // saltarse el cambio (ADR 015).
        await _products.SetActiveAsync(product, isActive, cancellationToken);

        return product;
    }
}
