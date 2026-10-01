namespace PirryLedger.Business.Domain;

// RF-NEG-04, RF-NEG-05 and RD-04: every invoice transition is resolved here.
// Paid and Cancelled deliberately have no outgoing transitions.
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
        : base($"The invoice cannot transition from {from} to {to}.")
    {
    }
}
