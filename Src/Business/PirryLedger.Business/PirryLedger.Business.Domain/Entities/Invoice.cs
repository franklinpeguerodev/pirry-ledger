using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Domain.Entities;

// RF-NEG-03: central business entity carrying the state machine state.
// The draft lives only in the client, so a row is never stored in Draft: the
// invoice is created in memory and issued in the same operation.
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

    // El contexto lo calcula quien conoce los items y los pagos; la maquina no
    // los lee de la entidad.
    public void TransitionTo(InvoiceState newState, InvoiceTransitionContext context)
    {
        State = InvoiceStateMachine.Transition(State, newState, context);
    }
}
