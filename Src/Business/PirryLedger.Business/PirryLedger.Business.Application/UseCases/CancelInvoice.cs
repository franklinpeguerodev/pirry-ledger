using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Core.Contracts.Permissions;

namespace PirryLedger.Business.Application.UseCases;

// RF-NEG-06 + ADR 011 + ADR 012: anular una factura. Desde Issued no exige
// permiso; desde Paid o InDebt consume la concesion CancelInvoice:<id> y, si no
// la hay, pide el permiso en nombre del usuario y rechaza. La linea de
// ConsumeAsync es el objetivo del defecto 4 del enunciado.
public sealed class CancelInvoice
{
    private readonly IInvoiceRepository _invoices;
    private readonly IPermissionAuthority _permissions;
    private readonly IPermissionRequestGateway _permissionRequests;

    public CancelInvoice(
        IInvoiceRepository invoices,
        IPermissionAuthority permissions,
        IPermissionRequestGateway permissionRequests)
    {
        _invoices = invoices;
        _permissions = permissions;
        _permissionRequests = permissionRequests;
    }

    public async Task<Invoice> ExecuteAsync(
        Guid invoiceId,
        Guid requesterUserId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetRequiredAsync(invoiceId, cancellationToken);

        if (invoice.State is InvoiceState.Paid or InvoiceState.InDebt)
        {
            var permissionName = PermissionNames.CancelInvoice(invoiceId);

            // Orden de ADR 011: transicion en memoria, consumo, guardado. Si el
            // consumo falla se rechaza antes de guardar la factura.
            invoice.Cancel();

            var consumed = true;

            if (!consumed)
            {
                var outcome = await _permissionRequests.RequestAsync(
                    requesterUserId,
                    permissionName,
                    cancellationToken);

                throw new CancellationPermissionRequiredException(
                    outcome == PermissionRequestOutcome.AlreadyPending);
            }
        }
        else
        {
            invoice.Cancel();
        }

        await _invoices.SaveChangesAsync(cancellationToken);

        return invoice;
    }
}
