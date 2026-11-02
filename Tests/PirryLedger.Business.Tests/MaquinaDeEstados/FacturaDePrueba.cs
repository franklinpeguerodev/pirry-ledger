using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Tests.MaquinaDeEstados;

// Construye la factura que cada prueba necesita sin tocar el estado por la
// espalda de la maquina: para llegar a un estado se recorre un camino de
// transiciones permitidas. Si una transicion permitida se rompe, estas pruebas
// lo notan en vez de esconderlo.
internal static class FacturaDePrueba
{
    internal const decimal Total = 100m;

    // Contexto que solo satisface la condicion de la fila que se esta probando.
    // Un contexto "todo verdadero" haria pasar las filas sin demostrar que la
    // condicion se evalua.
    internal static InvoiceTransitionContext Emitir() => new() { ItemCount = 1 };

    internal static InvoiceTransitionContext CobrarSinTransferencia() =>
        new() { TotalAmount = Total, PaidAmount = Total };

    internal static InvoiceTransitionContext CobrarConTransferencia() =>
        new() { TotalAmount = Total, PaidAmount = Total, HasTransfer = true };

    internal static InvoiceTransitionContext DejarComoDeuda() => new() { DebtorName = "Ana Perez" };

    internal static InvoiceTransitionContext EsDelivery() => new() { IsDelivery = true };

    internal static InvoiceTransitionContext SinCondiciones() => new();

    // Caminos canonicos: cada estado tiene una sola ruta desde Draft.
    internal static Invoice EnEstado(InvoiceState estado)
    {
        var factura = Invoice.Create();

        if (estado == InvoiceState.Draft)
        {
            return factura;
        }

        factura.TransitionTo(InvoiceState.Issued, Emitir());

        switch (estado)
        {
            case InvoiceState.Issued:
                return factura;
            case InvoiceState.Paid:
                factura.TransitionTo(InvoiceState.Paid, CobrarSinTransferencia());
                return factura;
            case InvoiceState.PendingVerification:
                factura.TransitionTo(InvoiceState.PendingVerification, CobrarConTransferencia());
                return factura;
            case InvoiceState.InDebt:
                factura.TransitionTo(InvoiceState.InDebt, DejarComoDeuda());
                return factura;
            case InvoiceState.Cancelled:
                factura.TransitionTo(InvoiceState.Cancelled, SinCondiciones());
                return factura;
            case InvoiceState.NotDelivered:
                factura.TransitionTo(InvoiceState.NotDelivered, EsDelivery());
                return factura;
            default:
                throw new InvalidOperationException($"Sin camino definido hacia {estado}.");
        }
    }
}
