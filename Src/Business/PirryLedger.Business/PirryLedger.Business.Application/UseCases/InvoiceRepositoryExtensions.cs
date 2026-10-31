using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

// La misma comprobacion de "existe o 404" en todos los casos de uso que
// empiezan por una factura concreta.
internal static class InvoiceRepositoryExtensions
{
    internal static async Task<Invoice> GetRequiredAsync(
        this IInvoiceRepository repository,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(invoiceId, cancellationToken);

        return invoice ?? throw new InvoiceNotFoundException();
    }
}
