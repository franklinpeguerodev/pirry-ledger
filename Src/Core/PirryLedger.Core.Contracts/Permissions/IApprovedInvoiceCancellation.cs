namespace PirryLedger.Core.Contracts.Permissions;

public interface IApprovedInvoiceCancellation
{
    Task<bool> TryCancelAsync(Guid invoiceId, CancellationToken cancellationToken);
}
