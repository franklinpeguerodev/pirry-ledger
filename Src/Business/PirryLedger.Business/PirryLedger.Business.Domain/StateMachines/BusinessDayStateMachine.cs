using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Domain.StateMachines;

public static class BusinessDayStateMachine
{
    private static readonly IReadOnlySet<(BusinessDayState From, BusinessDayState To)> AllowedTransitions =
        new HashSet<(BusinessDayState From, BusinessDayState To)>
        {
            (BusinessDayState.Open, BusinessDayState.Closed),
            (BusinessDayState.Closed, BusinessDayState.Open),
        };

    public static bool CanTransition(BusinessDayState from, BusinessDayState to) =>
        AllowedTransitions.Contains((from, to));

    public static BusinessDayState Transition(
        BusinessDayState from,
        BusinessDayState to,
        bool canReopen = true)
    {
        if (to == BusinessDayState.Open && (!CanTransition(from, to) || !canReopen))
        {
            throw new BusinessDayRuleViolationException(
                "ReopenRejected",
                "Solo se puede reabrir la última jornada cerrada antes de su límite.");
        }

        if (!CanTransition(from, to))
        {
            throw new BusinessDayRuleViolationException("AlreadyClosed", "La jornada ya está cerrada.");
        }

        return to;
    }
}
