using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Domain.StateMachines;

// RF-NEG-04, RF-NEG-05 y RD-04: aquí se resuelve toda transición de factura.
// Paid y Cancelled no tienen transiciones de salida a propósito.
public static class InvoiceStateMachine
{
    private static readonly IReadOnlySet<(InvoiceState From, InvoiceState To)> AllowedTransitions =
        new HashSet<(InvoiceState From, InvoiceState To)>
        {
            (InvoiceState.Draft, InvoiceState.Issued),
            (InvoiceState.Draft, InvoiceState.Cancelled),
            (InvoiceState.Issued, InvoiceState.Paid),
            (InvoiceState.Issued, InvoiceState.Cancelled),
        };

    public static bool CanTransition(InvoiceState from, InvoiceState to) =>
        AllowedTransitions.Contains((from, to));

    public static InvoiceState Transition(InvoiceState from, InvoiceState to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidInvoiceTransitionException(from, to);
        }

        return to;
    }
}

public sealed class InvalidInvoiceTransitionException : Exception
{
    public InvalidInvoiceTransitionException(InvoiceState from, InvoiceState to)
        : base($"La factura no puede pasar de {from} a {to}.")
    {
    }
}
