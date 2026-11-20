using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Domain.Entities;

public sealed class BusinessDay
{
    private BusinessDay()
    {
    }

    private BusinessDay(Guid id, Guid branchId, DateTime openedAtUtc)
    {
        Id = id;
        BranchId = branchId;
        OpenedAtUtc = openedAtUtc;
        BusinessDate = BusinessDayCalendar.DateAt(openedAtUtc);
        IsOpen = true;
    }

    public Guid Id { get; private set; }

    public Guid BranchId { get; private set; }

    public DateOnly BusinessDate { get; private set; }

    public DateTime OpenedAtUtc { get; private set; }

    // Se conserva el almacenamiento booleano existente; State se deriva de él.
    public bool IsOpen { get; private set; }

    public BusinessDayState State => IsOpen ? BusinessDayState.Open : BusinessDayState.Closed;

    public DateTime? LastClosedAtUtc { get; private set; }

    public Guid? LastClosedByUserId { get; private set; }

    public BusinessDayClosing? Closing { get; private set; }

    public bool IsReopened => IsOpen && LastClosedAtUtc is not null;

    public DateTime IssueDeadlineUtc => BusinessDayCalendar.IssueDeadline(BusinessDate);

    public static BusinessDay Open(Guid branchId, DateTime now)
    {
        BusinessDayCalendar.EnsureUtc(now);

        if (branchId == Guid.Empty)
        {
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        }

        return new BusinessDay(Guid.NewGuid(), branchId, now);
    }

    public void EnsureCanIssue(DateTime now)
    {
        BusinessDayCalendar.EnsureUtc(now);

        if (State != BusinessDayState.Open)
        {
            throw new BusinessDayRuleViolationException("ReopenRequired", "Debe reabrir la jornada para emitir otra factura.");
        }

        if (now >= IssueDeadlineUtc)
        {
            throw new BusinessDayRuleViolationException("ClosingRequired", "Debe cerrar la jornada pendiente antes de emitir otra factura.");
        }
    }

    public void Close(BusinessDayClosing closing, DateTime now, Guid userId)
    {
        BusinessDayCalendar.EnsureUtc(now);
        ArgumentNullException.ThrowIfNull(closing);

        // Validar la transición antes de modificar datos del cierre.
        var nextState = BusinessDayStateMachine.Transition(State, BusinessDayState.Closed);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El usuario es obligatorio.", nameof(userId));
        }

        Closing = closing;
        LastClosedAtUtc = now;
        LastClosedByUserId = userId;
        IsOpen = nextState == BusinessDayState.Open;
    }

    public void Reopen(DateTime now, bool hasLaterDay)
    {
        BusinessDayCalendar.EnsureUtc(now);

        var nextState = BusinessDayStateMachine.Transition(
            State,
            BusinessDayState.Open,
            canReopen: !hasLaterDay && now < IssueDeadlineUtc);

        IsOpen = nextState == BusinessDayState.Open;
    }
}
