using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Tests.Reglas;

// RF-NEG-02: emitir es la primera funcionalidad separable. La factura nace en
// memoria (Draft), se emite y se guarda ya emitida. El total se calcula desde
// los items: nunca llega desde fuera.
public sealed class PruebasDeEmision
{
    private static readonly DateTime Ahora = new(2026, 10, 29, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void EmitirGuardaLaFacturaConSusItemsYSuTotal()
    {
        // Arrange
        var factura = Invoice.Create();
        var items = new[]
        {
            InvoiceItem.Create("Hamburguesa doble", 250m, 2),
            InvoiceItem.Create("Refresco", 75m, 1),
        };

        // Act
        factura.Issue(items, isDelivery: false, Ahora);

        // Assert
        Assert.Equal(InvoiceState.Issued, factura.State);
        Assert.Equal(2, factura.Items.Count);
        Assert.Equal(575m, factura.Total);
        Assert.Equal(Ahora, factura.IssuedAtUtc);
    }

    [Fact]
    public void EmitirUnaFacturaDeDeliveryGuardaLaMarca()
    {
        // Arrange
        var factura = Invoice.Create();
        var items = new[] { InvoiceItem.Create("Combo", 400m, 1) };

        // Act
        factura.Issue(items, isDelivery: true, Ahora);

        // Assert
        Assert.True(factura.IsDelivery);
        Assert.Equal(InvoiceState.Issued, factura.State);
    }

    // La comprobacion de "al menos un item" vive solo en la maquina (defecto 3).
    // El agregado no la repite: por eso el rechazo es
    // InvoiceConditionNotMetException y no una regla propia.
    [Fact]
    public void EmitirSinItemsSeRechazaYNoCreaNada()
    {
        // Arrange
        var factura = Invoice.Create();

        // Act + Assert
        Assert.Throws<InvoiceConditionNotMetException>(
            () => factura.Issue(Array.Empty<InvoiceItem>(), isDelivery: false, Ahora));
        Assert.Equal(InvoiceState.Draft, factura.State);
        Assert.Empty(factura.Items);
    }

    [Fact]
    public void UnItemConCantidadCeroSeRechaza()
    {
        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(
            () => InvoiceItem.Create("Hamburguesa", 250m, 0));
    }

    [Fact]
    public void UnItemConPrecioNegativoSeRechaza()
    {
        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(
            () => InvoiceItem.Create("Hamburguesa", -1m, 1));
    }

    // Regla de Franklin: dos decimales exactos siempre. No se redondea en
    // silencio: un precio con tres decimales se rechaza.
    [Fact]
    public void UnPrecioConTresDecimalesSeRechaza()
    {
        // Act + Assert
        Assert.Throws<InvoiceRuleViolationException>(
            () => InvoiceItem.Create("Hamburguesa", 250.005m, 1));
    }
}
