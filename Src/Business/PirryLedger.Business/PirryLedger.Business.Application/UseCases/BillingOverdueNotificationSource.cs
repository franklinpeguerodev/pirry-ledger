using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Core.Contracts.Notifications;

namespace PirryLedger.Business.Application.UseCases;

public sealed class BillingOverdueNotificationSource(IOverdueBillingRepository repository) : IOverdueNotificationSource
{
    public bool Handles(NotificationEventKind kind) => kind is NotificationEventKind.TransferOverdue or NotificationEventKind.BusinessDayOverdue;

    public async Task<IReadOnlyList<OverdueNotificationCandidate>> FindCandidatesAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        BusinessDayCalendar.EnsureUtc(nowUtc);
        var payments = await repository.FindPendingTransfersAsync(nowUtc.AddDays(-4), cancellationToken);
        var days = await repository.FindOpenDaysAsync(BusinessDayCalendar.DateAt(nowUtc), cancellationToken);
        return payments.Where(payment => IsDue(payment, nowUtc))
            .Select(payment => new OverdueNotificationCandidate(NotificationEventKind.TransferOverdue, payment.Id, payment.RegisteredAtUtc.AddDays(4)))
            .Concat(days.Where(day => OverdueBillingRules.IsBusinessDayDue(day.Day.IsOpen, day.Day.BusinessDate, nowUtc))
                .Select(day => new OverdueNotificationCandidate(NotificationEventKind.BusinessDayOverdue, day.Day.Id, day.Day.IssueDeadlineUtc)))
            .ToArray();
    }

    public Task<NotificationEvent?> RevalidateAsync(OverdueNotificationCandidate candidate, DateTime nowUtc, CancellationToken cancellationToken = default) =>
        FindEventAsync(candidate.Kind, candidate.SourceId, nowUtc, true, cancellationToken);

    public async Task<bool> IsRelevantAsync(NotificationEventKind kind, Guid sourceId, DateTime nowUtc, CancellationToken cancellationToken = default) =>
        await FindEventAsync(kind, sourceId, nowUtc, false, cancellationToken) is not null;

    private async Task<NotificationEvent?> FindEventAsync(NotificationEventKind kind, Guid id, DateTime now, bool locked, CancellationToken ct)
    {
        if (kind == NotificationEventKind.TransferOverdue)
        {
            var payment = await repository.FindTransferAsync(id, locked, ct);
            return payment is not null && IsDue(payment, now)
                ? new NotificationEvent(kind, id, $"Transferencia de {payment.Amount:F2} en factura {payment.InvoiceId}, sucursal {payment.BranchName}, pendiente de verificar por más de cuatro días.", BranchId: payment.BranchId)
                : null;
        }
        if (kind == NotificationEventKind.BusinessDayOverdue)
        {
            var day = await repository.FindDayAsync(id, locked, ct);
            return day is not null && OverdueBillingRules.IsBusinessDayDue(day.Day.IsOpen, day.Day.BusinessDate, now)
                ? new NotificationEvent(kind, id, $"La jornada {day.Day.BusinessDate:yyyy-MM-dd} de {day.BranchName} sigue abierta después de su límite de cierre.", BranchId: day.Day.BranchId)
                : null;
        }
        throw new ArgumentException("El tipo de aviso no corresponde a Billing.");
    }

    private static bool IsDue(OverdueTransfer payment, DateTime now) => OverdueBillingRules.IsTransferDue(
        payment.Method, payment.Status, payment.InvoiceState, payment.RegisteredAtUtc, now);
}
