namespace PirryLedger.Business.Domain.StateMachines;

// Los datos que la maquina necesita para comprobar la condicion de una fila.
// La maquina no conoce la entidad ni sus colecciones: recibe los hechos de la
// tentativa, y el caso de uso los calcula a partir de la factura. Asi la
// maquina se prueba sin agregado, sin base de datos y sin reloj (RD-12).
//
// Los importes son los de la tentativa, no los de la factura guardada: en la
// emision el total sale de los items y en el cobro, de los pagos acumulados.
public sealed record InvoiceTransitionContext
{
    // Cuantos items lleva la factura. Draft -> Issued exige al menos uno.
    public int ItemCount { get; init; }

    // Si la factura se entrega a domicilio. Solo una de delivery puede quedar
    // en NotDelivered.
    public bool IsDelivery { get; init; }

    // Nombre de la persona que queda debiendo. Obligatorio para toda deuda.
    public string? DebtorName { get; init; }

    // Total de la factura y suma de los pagos o abonos registrados.
    public decimal TotalAmount { get; init; }

    public decimal PaidAmount { get; init; }

    // Si alguno de los pagos es una transferencia: la factura pasa a
    // PendingVerification en vez de Paid.
    public bool HasTransfer { get; init; }
}
