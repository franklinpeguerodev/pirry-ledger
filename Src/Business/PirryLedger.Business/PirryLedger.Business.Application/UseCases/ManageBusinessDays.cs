using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Application.UseCases;

public sealed class ManageBusinessDays(IBusinessDayRepository days, IBranchScopeAccessor scope,
    IPendingInvoiceCancellations cancellations, IClock clock)
{
    public Task<BusinessDay?> CurrentAsync(Guid branch, CancellationToken ct = default)
    {
        Authorize(branch);
        return days.LatestAsync(branch, ct);
    }

    public Task<Invoice> IssueAsync(Guid branch, Func<Guid, Task<Invoice>> issue, CancellationToken ct) =>
        InBranch(branch, async () =>
        {
            var day = await days.LatestAsync(branch, ct);
            var now = clock.UtcNow;
            if (day is null || (!day.IsOpen && now >= day.IssueDeadlineUtc))
            {
                day = BusinessDay.Open(branch, now);
                await days.AddAsync(day, ct);
            }
            day.EnsureCanIssue(now);
            return await issue(day.Id);
        }, ct);

    public Task<BusinessDay> CloseAsync(Guid branch, Guid id, CancellationToken ct = default) =>
        InBranch(branch, async () =>
        {
            var day = await Required(branch, id, ct);
            var invoices = await days.InvoicesAsync(id, ct);
            var pending = await cancellations.FindAsync(invoices.Select(x => x.Id).ToArray(), ct);
            var unfinished = invoices.Where(x => x.State == InvoiceState.Issued && !pending.Contains(x.Id))
                .Select(x => new UnfinishedInvoice(x.Id, x.IssuedByUserId, x.IssuedByUserName)).ToArray();
            if (unfinished.Length > 0) throw new UnfinishedInvoicesException(unfinished);
            day.Close(Calculate(invoices, pending), clock.UtcNow, scope.UserId!.Value);
            await days.SaveAsync(ct);
            return day;
        }, ct);

    public Task<BusinessDay> ReopenAsync(Guid branch, Guid id, CancellationToken ct = default) =>
        InBranch(branch, async () =>
        {
            var day = await Required(branch, id, ct);
            day.Reopen(clock.UtcNow, await days.HasLaterAsync(day, ct));
            await days.SaveAsync(ct);
            return day;
        }, ct);

    private Task<T> InBranch<T>(Guid branch, Func<Task<T>> action, CancellationToken ct)
    {
        Authorize(branch);
        return days.InBranchTransactionAsync(branch, action, ct);
    }

    private void Authorize(Guid branch)
    {
        if (branch == Guid.Empty || scope.UserId is null || (!scope.IsGlobal && scope.BranchId != branch))
            throw new BusinessDayRuleViolationException("BranchDenied", "No tiene acceso a esa sucursal.");
    }

    private async Task<BusinessDay> Required(Guid branch, Guid id, CancellationToken ct) =>
        await days.FindAsync(id, branch, ct)
        ?? throw new BusinessDayRuleViolationException("NotFound", "La jornada indicada no existe.");

    private static BusinessDayClosing Calculate(IReadOnlyList<Invoice> invoices, IReadOnlySet<Guid> pending)
    {
        var included = invoices.Where(x => !pending.Contains(x.Id)
            && x.State is not (InvoiceState.Cancelled or InvoiceState.NotDelivered)).ToArray();
        var payments = included.SelectMany(x => x.Payments).Where(x => x.Kind == PaymentKind.Sale).ToArray();
        return new BusinessDayClosing(included.Sum(x => x.Total),
            payments.Where(x => x.Method == PaymentMethod.Cash && x.Status == PaymentStatus.Confirmed).Sum(x => x.Amount),
            payments.Where(x => x.Method == PaymentMethod.Transfer && x.Status == PaymentStatus.Confirmed).Sum(x => x.Amount),
            payments.Where(x => x.Method == PaymentMethod.Transfer && x.Status == PaymentStatus.Registered).Sum(x => x.Amount),
            included.Where(x => x.DebtorName is not null).Sum(x => x.Total - x.Payments
                .Where(p => p.Kind == PaymentKind.Sale && p.Status != PaymentStatus.Rejected).Sum(p => p.Amount)),
            invoices.Where(x => pending.Contains(x.Id)).Sum(x => x.Total),
            invoices.Where(x => !pending.Contains(x.Id) && x.State == InvoiceState.Cancelled).Sum(x => x.Total),
            invoices.Where(x => !pending.Contains(x.Id) && x.State == InvoiceState.NotDelivered).Sum(x => x.Total));
    }
}

public sealed record UnfinishedInvoice(Guid InvoiceId, Guid IssuerId, string IssuerName);
public sealed class UnfinishedInvoicesException(IReadOnlyList<UnfinishedInvoice> invoices)
    : Exception("No puede cerrar: hay facturas inconclusas en la sucursal.")
{
    public IReadOnlyList<UnfinishedInvoice> Invoices { get; } = invoices;
}
