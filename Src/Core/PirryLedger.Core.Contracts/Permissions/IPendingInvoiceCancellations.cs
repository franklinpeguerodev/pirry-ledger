namespace PirryLedger.Core.Contracts.Permissions;

// Solo identificadores solicitados: cada módulo consulta sus propios datos.
public interface IPendingInvoiceCancellations
{
    Task<IReadOnlySet<Guid>> FindAsync(IReadOnlyCollection<Guid> invoiceIds, CancellationToken cancellationToken);
}
