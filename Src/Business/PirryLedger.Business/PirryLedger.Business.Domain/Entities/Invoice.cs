using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Domain.Entities;

// RF-NEG-03: central business entity carrying the state machine state.
// The draft lives only in the client, so a row is never stored in Draft: the
// invoice is created in memory and issued in the same operation.
public sealed class Invoice
{
    private readonly List<InvoiceItem> _items = new();

    private readonly List<InvoicePayment> _payments = new();

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

    // Igual que Items: EF Core necesita una coleccion escribible para cargar los
    // pagos. Solo los metodos del agregado la modifican.
    public ICollection<InvoicePayment> Payments => _payments;

    public decimal Total => _items.Sum(item => item.LineTotal);

    // Una transferencia rechazada no cuenta: la factura sigue debiendo ese dinero.
    public decimal PaidAmount =>
        _payments.Where(payment => payment.Status != PaymentStatus.Rejected)
            .Sum(payment => payment.Amount);

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

    // Issued -> Paid o Issued -> PendingVerification. La hora y el usuario viven
    // en cada pago, no se pasan otra vez: dos fuentes para el mismo dato
    // terminarian contradiciendose.
    //
    // La cobertura del total y la presencia de una transferencia NO se repiten
    // aqui: son las condiciones de las filas Issued -> Paid e
    // Issued -> PendingVerification de la maquina (RD-04). El agregado solo
    // aplica las reglas de dinero que la maquina no puede ver.
    public void Collect(IReadOnlyCollection<InvoicePayment> payments)
    {
        ArgumentNullException.ThrowIfNull(payments);

        var paidAmount = payments.Sum(payment => payment.Amount);

        if (paidAmount > Total)
        {
            throw new InvoiceRuleViolationException(
                "Los pagos no pueden exceder el total de la factura.");
        }

        EnsureDeliveryDoesNotRegisterReceived(payments);
        EnsureCashRegistersReceived(payments, IsDelivery);

        var hasTransfer = payments.Any(payment => payment.Method == PaymentMethod.Transfer);

        var target = hasTransfer ? InvoiceState.PendingVerification : InvoiceState.Paid;

        var context = new InvoiceTransitionContext
        {
            TotalAmount = Total,
            PaidAmount = paidAmount,
            HasTransfer = hasTransfer,
        };

        State = InvoiceStateMachine.Transition(State, target, context);

        foreach (var payment in payments)
        {
            payment.AttachTo(Id);
            _payments.Add(payment);
        }
    }

    // En delivery no se sabe cuanto entrego ni cuanto devolvio el courier, asi
    // que un recibido o un cambio se rechazan en vez de ignorarse.
    private void EnsureDeliveryDoesNotRegisterReceived(
        IReadOnlyCollection<InvoicePayment> payments)
    {
        if (!IsDelivery)
        {
            return;
        }

        if (payments.Any(payment =>
                payment.ReceivedAmount is not null || payment.ChangeAmount is not null))
        {
            throw new InvoiceRuleViolationException(
                "Una factura de delivery no registra el monto recibido ni el cambio.");
        }
    }

    // En mostrador el cajero siempre entrega el cambio, asi que el recibido es
    // obligatorio. En delivery no se registra. La transferencia no lo lleva.
    private static void EnsureCashRegistersReceived(
        IReadOnlyCollection<InvoicePayment> payments,
        bool isDelivery)
    {
        if (isDelivery)
        {
            return;
        }

        if (payments.Any(payment =>
                payment.Method == PaymentMethod.Cash && payment.ReceivedAmount is null))
        {
            throw new InvoiceRuleViolationException(
                "El cobro en efectivo de mostrador exige el monto recibido.");
        }
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
