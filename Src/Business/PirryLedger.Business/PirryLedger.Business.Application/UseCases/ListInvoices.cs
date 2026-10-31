using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: consultar las facturas. Cualquier sesion valida puede leerlas; el
// historial de edicion es solo del Administrador (decision 9).
public sealed class ListInvoices
{
    private readonly IInvoiceRepository _invoices;

    public ListInvoices(IInvoiceRepository invoices)
    {
        _invoices = invoices;
    }

    public Task<IReadOnlyList<Invoice>> ExecuteAsync(
        CancellationToken cancellationToken = default) =>
        _invoices.ListAsync(cancellationToken);
}
