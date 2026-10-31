using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-02: leer el historial de edicion de una factura. Solo el
// Administrador; el filtro lo declara en RoleRequirements (decision 9).
public sealed class ListInvoiceEdits
{
    private readonly IInvoiceRepository _invoices;

    public ListInvoiceEdits(IInvoiceRepository invoices)
    {
        _invoices = invoices;
    }

    public async Task<IReadOnlyList<InvoiceEdit>> ExecuteAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        return invoice.Edits.ToList();
    }
}
