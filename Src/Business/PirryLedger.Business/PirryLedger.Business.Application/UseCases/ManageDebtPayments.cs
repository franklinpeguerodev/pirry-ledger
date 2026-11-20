using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

public sealed class ManageDebtPayments
{
    private readonly IDebtRepository _debts;
    private readonly IBranchScopeAccessor _scope;
    private readonly IPendingInvoiceCancellations _cancellations;
    private readonly IClock _clock;

    public ManageDebtPayments(IDebtRepository debts, IBranchScopeAccessor scope,
        IPendingInvoiceCancellations cancellations, IClock clock)
    {
        _debts = debts;
        _scope = scope;
        _cancellations = cancellations;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DebtSummary>> ListAsync(Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var debts = await _debts.ListAsync(BranchFilter(branchId), cancellationToken);
        return debts.Select(DebtSummary.From).ToList();
    }

    public async Task<DebtSummary> GetAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await RequiredAsync(invoiceId, cancellationToken);
        return DebtSummary.From(invoice);
    }

    public async Task<Invoice> HistoryAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        return await RequiredAsync(invoiceId, cancellationToken);
    }

    public Task<Invoice> RegisterAsync(Guid invoiceId, decimal amount, PaymentMethod method,
        CancellationToken cancellationToken = default)
    {
        var branch = BranchFilter();
        return _debts.InInvoiceTransactionAsync(invoiceId, branch, async invoice =>
        {
            var pending = await _cancellations.FindAsync([invoiceId], cancellationToken);

            if (pending.Contains(invoiceId))
            {
                throw new InvoiceRuleViolationException("No se puede abonar una factura con anulación pendiente.");
            }

            invoice.RegisterDebtPayment(amount, _clock.UtcNow, _scope.UserId!.Value, method);
            await _debts.SaveAsync(cancellationToken);
            return invoice;
        }, cancellationToken);
    }

    public Task<Invoice> ConfirmAsync(Guid invoiceId, Guid paymentId, CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        return _debts.InInvoiceTransactionAsync(invoiceId, BranchFilter(), async invoice =>
        {
            invoice.ConfirmDebtTransfer(paymentId, _clock.UtcNow, _scope.UserId!.Value);
            await _debts.SaveAsync(cancellationToken);
            return invoice;
        }, cancellationToken);
    }

    public Task<Invoice> RejectAsync(Guid invoiceId, Guid paymentId, string reason,
        CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        return _debts.InInvoiceTransactionAsync(invoiceId, BranchFilter(), async invoice =>
        {
            invoice.RejectDebtTransfer(paymentId, _clock.UtcNow, _scope.UserId!.Value, reason);
            await _debts.SaveAsync(cancellationToken);
            return invoice;
        }, cancellationToken);
    }

    private async Task<Invoice> RequiredAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        await _debts.FindAsync(invoiceId, BranchFilter(), cancellationToken)
        ?? throw new InvoiceNotFoundException();

    private Guid? BranchFilter(Guid? requestedBranch = null)
    {
        if (_scope.UserId is null || (!_scope.IsGlobal && (_scope.BranchId is null
                || requestedBranch is not null && requestedBranch != _scope.BranchId)))
        {
            throw new DebtAccessDeniedException();
        }

        return _scope.IsGlobal ? requestedBranch : _scope.BranchId;
    }

    private void EnsureAdministrator()
    {
        if (!_scope.IsGlobal || _scope.UserId is null)
        {
            throw new DebtAccessDeniedException();
        }
    }
}
