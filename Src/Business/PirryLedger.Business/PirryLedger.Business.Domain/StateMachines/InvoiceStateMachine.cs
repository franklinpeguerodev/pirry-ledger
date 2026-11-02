using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Domain.StateMachines;

// RF-NEG-03, RF-NEG-04, RF-NEG-05 y RD-04: aqui se resuelve toda transicion de
// factura. La tabla AllowedTransitions es la unica fuente de verdad; una
// transicion que no esta en ella se rechaza y el estado no cambia.
//
// Cancelled y NotDelivered no tienen transiciones de salida a proposito.
//
// La consulta del permiso de anulacion NO vive aqui: depende de la factura
// concreta y la resuelve el caso de uso con una sola linea. La maquina solo
// decide si el par (desde, hacia) existe y si su condicion se cumple.
public static class InvoiceStateMachine
{
    private static readonly IReadOnlySet<(InvoiceState From, InvoiceState To)> AllowedTransitions =
        new HashSet<(InvoiceState From, InvoiceState To)>
        {
            (InvoiceState.Draft, InvoiceState.Issued),
            (InvoiceState.Issued, InvoiceState.Paid),
            (InvoiceState.Issued, InvoiceState.PendingVerification),
            (InvoiceState.Issued, InvoiceState.InDebt),
            (InvoiceState.Issued, InvoiceState.Cancelled),
            (InvoiceState.Issued, InvoiceState.NotDelivered),
            (InvoiceState.PendingVerification, InvoiceState.Paid),
            (InvoiceState.PendingVerification, InvoiceState.InDebt),
            (InvoiceState.InDebt, InvoiceState.Paid),
            (InvoiceState.Paid, InvoiceState.Cancelled),
            (InvoiceState.InDebt, InvoiceState.Cancelled),
            (InvoiceState.Cancelled, InvoiceState.Issued),
        };

    public static bool CanTransition(InvoiceState from, InvoiceState to) =>
        AllowedTransitions.Contains((from, to));

    // Internal: recibe el contexto de la tentativa, que solo el dominio puede
    // construir. Las pruebas del dominio la ven por InternalsVisibleTo.
    internal static InvoiceState Transition(
        InvoiceState from,
        InvoiceState to,
        InvoiceTransitionContext context)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidInvoiceTransitionException(from, to);
        }

        EnsureConditionIsMet(from, to, context);

        return to;
    }

    // Una comprobacion por fila, aislada en su propio metodo para que se pueda
    // quitar sola: el defecto 3 del enunciado elimina la de Draft -> Issued.
    private static void EnsureConditionIsMet(
        InvoiceState from,
        InvoiceState to,
        InvoiceTransitionContext context)
    {
        switch (from, to)
        {
            case (InvoiceState.Draft, InvoiceState.Issued):
                EnsureHasAtLeastOneItem(from, to, context);
                break;

            case (InvoiceState.Issued, InvoiceState.Paid):
                EnsurePaymentsCoverTheTotal(from, to, context);
                EnsureNoTransferIsPending(from, to, context);
                break;

            case (InvoiceState.Issued, InvoiceState.PendingVerification):
                EnsureHasTransfer(from, to, context);
                break;

            case (InvoiceState.Issued, InvoiceState.InDebt):
            case (InvoiceState.PendingVerification, InvoiceState.InDebt):
                EnsureDebtorNameIsPresent(from, to, context);
                break;

            case (InvoiceState.Issued, InvoiceState.NotDelivered):
                EnsureIsDelivery(from, to, context);
                break;

            case (InvoiceState.InDebt, InvoiceState.Paid):
                EnsurePaymentsCoverTheTotal(from, to, context);
                break;
        }
    }

    private static void EnsureHasAtLeastOneItem(
        InvoiceState from,
        InvoiceState to,
        InvoiceTransitionContext context)
    {
        if (context.ItemCount < 1)
        {
            throw new InvoiceConditionNotMetException(
                from,
                to,
                "La factura debe tener al menos un ítem para emitirse.");
        }
    }

    private static void EnsurePaymentsCoverTheTotal(
        InvoiceState from,
        InvoiceState to,
        InvoiceTransitionContext context)
    {
        if (context.PaidAmount < context.TotalAmount)
        {
            throw new InvoiceConditionNotMetException(
                from,
                to,
                "Los pagos no cubren el total de la factura.");
        }
    }

    private static void EnsureNoTransferIsPending(
        InvoiceState from,
        InvoiceState to,
        InvoiceTransitionContext context)
    {
        if (context.HasTransfer)
        {
            throw new InvoiceConditionNotMetException(
                from,
                to,
                "Una factura con transferencia debe verificarse antes de marcarse pagada.");
        }
    }

    private static void EnsureHasTransfer(
        InvoiceState from,
        InvoiceState to,
        InvoiceTransitionContext context)
    {
        if (!context.HasTransfer)
        {
            throw new InvoiceConditionNotMetException(
                from,
                to,
                "La verificación pendiente exige al menos una transferencia.");
        }
    }

    private static void EnsureDebtorNameIsPresent(
        InvoiceState from,
        InvoiceState to,
        InvoiceTransitionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.DebtorName))
        {
            throw new InvoiceConditionNotMetException(
                from,
                to,
                "La deuda exige el nombre de la persona.");
        }
    }

    private static void EnsureIsDelivery(
        InvoiceState from,
        InvoiceState to,
        InvoiceTransitionContext context)
    {
        if (!context.IsDelivery)
        {
            throw new InvoiceConditionNotMetException(
                from,
                to,
                "Solo una factura de delivery puede marcarse como no entregada.");
        }
    }
}

public sealed class InvalidInvoiceTransitionException : Exception
{
    public InvalidInvoiceTransitionException(InvoiceState from, InvoiceState to)
        : base($"La factura no puede pasar de {from} a {to}.")
    {
        From = from;
        To = to;
    }

    public InvoiceState From { get; }

    public InvoiceState To { get; }
}

// El par existe en la tabla, pero la condicion de esa fila no se cumple. Es un
// rechazo controlado distinto de una transicion prohibida.
public sealed class InvoiceConditionNotMetException : Exception
{
    public InvoiceConditionNotMetException(InvoiceState from, InvoiceState to, string message)
        : base(message)
    {
        From = from;
        To = to;
    }

    public InvoiceState From { get; }

    public InvoiceState To { get; }
}
