using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Ports;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Tests.Reglas;

// RF-NEG-02: editar los items de una factura emitida. Se puede agregar, quitar
// o cambiar mientras no este pagada; fuera de Issued esta congelada. Cada
// edicion queda en el historial y avisa al Administrador.
public sealed class PruebasDeEdicion
{
    private static readonly DateTime Ahora = new(2026, 10, 29, 18, 0, 0, DateTimeKind.Utc);

    private static readonly Guid Cajero = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Invoice FacturaEmitida()
    {
        var factura = Invoice.Create();
        factura.Issue(new[] { InvoiceItem.Create("Combo", 500m, 1) }, isDelivery: false, Ahora);
        return factura;
    }

    [Fact]
    public void EditarLosItemsDeUnaFacturaEmitidaGuardaElHistorial()
    {
        // Arrange
        var factura = FacturaEmitida();
        var notifier = new ContadorDeNotificaciones();
        var nuevos = new[] { InvoiceItem.Create("Combo", 500m, 1), InvoiceItem.Create("Refresco", 75m, 1) };

        // Act
        factura.EditItems(nuevos, Ahora, Cajero, notifier);

        // Assert
        Assert.Single(factura.Edits);
        Assert.Equal(575m, factura.Total);
        Assert.Equal(Cajero, factura.Edits.First().EditedByUserId);
    }

    [Fact]
    public void EditarPermiteAgregarItemsMientrasNoEstePagada()
    {
        // Arrange
        var factura = FacturaEmitida();
        var notifier = new ContadorDeNotificaciones();

        // Act
        factura.EditItems(new[] { InvoiceItem.Create("Combo", 500m, 2) }, Ahora, Cajero, notifier);

        // Assert
        Assert.Equal(1000m, factura.Total);
        Assert.Single(factura.Edits);
    }

    [Fact]
    public void EditarLosItemsFueraDeIssuedSeRechaza()
    {
        // Arrange
        var factura = FacturaEmitida();
        factura.Collect(new[] { InvoicePayment.Cash(500m, 500m, Ahora, Cajero) });
        var notifier = new ContadorDeNotificaciones();

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(
            () => factura.EditItems(new[] { InvoiceItem.Create("Combo", 500m, 1) }, Ahora, Cajero, notifier));
        Assert.Equal(InvoiceState.Paid, factura.State);
        Assert.Empty(factura.Edits);
    }

    [Fact]
    public void EditarSinItemsSeRechazaYNoCambiaLaFactura()
    {
        // Arrange
        var factura = FacturaEmitida();
        var notifier = new ContadorDeNotificaciones();

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(
            () => factura.EditItems(Array.Empty<InvoiceItem>(), Ahora, Cajero, notifier));
        Assert.Equal(InvoiceState.Issued, factura.State);
        Assert.Single(factura.Items);
        Assert.Equal(500m, factura.Total);
        Assert.Empty(factura.Edits);
        Assert.Equal(0, notifier.Llamadas);
    }

    [Fact]
    public void UnaFacturaEnVerificacionPendienteNoSeEdita()
    {
        // Arrange
        var factura = FacturaEmitida();
        factura.Collect(new[] { InvoicePayment.Transfer(500m, Ahora, Cajero) });
        var notifier = new ContadorDeNotificaciones();

        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(
            () => factura.EditItems(new[] { InvoiceItem.Create("Combo", 500m, 1) }, Ahora, Cajero, notifier));
        Assert.Empty(factura.Edits);
    }

    [Fact]
    public void EditarAvisaAlAdministradorUnaVez()
    {
        // Arrange
        var factura = FacturaEmitida();
        var notifier = new ContadorDeNotificaciones();

        // Act
        factura.EditItems(new[] { InvoiceItem.Create("Combo", 500m, 1) }, Ahora, Cajero, notifier);

        // Assert
        Assert.Equal(1, notifier.Llamadas);
    }

    private sealed class ContadorDeNotificaciones : IInvoiceEditedNotifier
    {
        public int Llamadas { get; private set; }

        public void Notify(Invoice invoice, Guid editedByUserId, DateTime editedAtUtc) => Llamadas++;
    }
}
