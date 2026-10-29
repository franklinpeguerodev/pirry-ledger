using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Domain.Entities;

// RF-NEG-03: central business entity carrying the state machine state.
// The draft lives only in the client, so a row is never stored in Draft: the
// invoice is created in memory and issued in the same operation.
public sealed class Invoice
{
    private readonly List<InvoiceItem> _items = new();

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

    // Se fija al emitir. Es la fecha de la venta y la que usaran los reportes.
    public DateTime IssuedAtUtc { get; private set; }

    public bool IsDelivery { get; private set; }

    // ICollection y no IReadOnlyList: EF Core necesita una coleccion escribible
    // para cargar las lineas. Los metodos del agregado son los unicos que la
    // modifican.
    public ICollection<InvoiceItem> Items => _items;

    public decimal Total => _items.Sum(item => item.LineTotal);

    public static Invoice Create() => new(Guid.NewGuid());

    // Draft -> Issued. El contexto se arma con los items que llegan, no con los
    // que ya tiene la factura (todavia no tiene ninguno). Si la transicion se
    // rechaza, el agregado no cambia: los items se agregan despues.
    public void Issue(IReadOnlyCollection<InvoiceItem> items, bool isDelivery, DateTime issuedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(items);

        var context = new InvoiceTransitionContext { ItemCount = items.Count };

        State = InvoiceStateMachine.Transition(State, InvoiceState.Issued, context);

        foreach (var item in items)
        {
            item.AttachTo(Id);
            _items.Add(item);
        }

        IsDelivery = isDelivery;
        IssuedAtUtc = issuedAtUtc;
    }

    // Costura interna: la usan las pruebas de la maquina, que comprueban la
    // tabla fila por fila con un contexto a mano. En produccion el unico camino
    // es TransitionTo(estado), que calcula el contexto desde los items y los
    // pagos y no puede mentir.
    internal void TransitionTo(InvoiceState newState, InvoiceTransitionContext context)
    {
        State = InvoiceStateMachine.Transition(State, newState, context);
    }
}
