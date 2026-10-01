namespace PirryLedger.Business.Domain;

// RF-NEG-03: central business entity carrying the state machine state.
public sealed class Invoice
{
    private Invoice()
    {
    }

    private Invoice(Guid id)
    {
        Id = id;
        State = InvoiceState.Draft;
    }

    public Guid Id { get; private set; }

    public InvoiceState State { get; private set; }

    public static Invoice Create() => new(Guid.NewGuid());

    public void TransitionTo(InvoiceState newState)
    {
        State = InvoiceStateMachine.Transition(State, newState);
    }
}
