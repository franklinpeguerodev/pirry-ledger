using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Tests.MaquinaDeEstados;

// RF-NEG-04 y RF-NEG-05: una prueba por cada fila de la tabla de transiciones
// prohibidas de docs/maquina-de-estados.md. Cada rechazo comprueba tambien que
// el estado no cambio.
public sealed class PruebasDeTransicionesProhibidas
{
    // Las 38 filas prohibidas: 49 pares posibles menos las 11 permitidas,
    // incluidas las 7 que dejan el estado igual.
    public static TheoryData<InvoiceState, InvoiceState> TransicionesProhibidas => new()
    {
        // Desde Draft (6)
        { InvoiceState.Draft, InvoiceState.Draft },
        { InvoiceState.Draft, InvoiceState.PendingVerification },
        { InvoiceState.Draft, InvoiceState.Paid },
        { InvoiceState.Draft, InvoiceState.InDebt },
        { InvoiceState.Draft, InvoiceState.Cancelled },
        { InvoiceState.Draft, InvoiceState.NotDelivered },

        // Desde Issued (2)
        { InvoiceState.Issued, InvoiceState.Draft },
        { InvoiceState.Issued, InvoiceState.Issued },

        // Desde PendingVerification (5)
        { InvoiceState.PendingVerification, InvoiceState.Draft },
        { InvoiceState.PendingVerification, InvoiceState.Issued },
        { InvoiceState.PendingVerification, InvoiceState.PendingVerification },
        { InvoiceState.PendingVerification, InvoiceState.Cancelled },
        { InvoiceState.PendingVerification, InvoiceState.NotDelivered },

        // Desde Paid (6)
        { InvoiceState.Paid, InvoiceState.Draft },
        { InvoiceState.Paid, InvoiceState.Issued },
        { InvoiceState.Paid, InvoiceState.PendingVerification },
        { InvoiceState.Paid, InvoiceState.Paid },
        { InvoiceState.Paid, InvoiceState.InDebt },
        { InvoiceState.Paid, InvoiceState.NotDelivered },

        // Desde InDebt (5)
        { InvoiceState.InDebt, InvoiceState.Draft },
        { InvoiceState.InDebt, InvoiceState.Issued },
        { InvoiceState.InDebt, InvoiceState.PendingVerification },
        { InvoiceState.InDebt, InvoiceState.InDebt },
        { InvoiceState.InDebt, InvoiceState.NotDelivered },

        // Desde Cancelled: todas (7)
        { InvoiceState.Cancelled, InvoiceState.Draft },
        { InvoiceState.Cancelled, InvoiceState.Issued },
        { InvoiceState.Cancelled, InvoiceState.PendingVerification },
        { InvoiceState.Cancelled, InvoiceState.Paid },
        { InvoiceState.Cancelled, InvoiceState.InDebt },
        { InvoiceState.Cancelled, InvoiceState.Cancelled },
        { InvoiceState.Cancelled, InvoiceState.NotDelivered },

        // Desde NotDelivered: todas (7)
        { InvoiceState.NotDelivered, InvoiceState.Draft },
        { InvoiceState.NotDelivered, InvoiceState.Issued },
        { InvoiceState.NotDelivered, InvoiceState.PendingVerification },
        { InvoiceState.NotDelivered, InvoiceState.Paid },
        { InvoiceState.NotDelivered, InvoiceState.InDebt },
        { InvoiceState.NotDelivered, InvoiceState.Cancelled },
        { InvoiceState.NotDelivered, InvoiceState.NotDelivered },
    };

    [Theory]
    [MemberData(nameof(TransicionesProhibidas))]
    public void UnaTransicionProhibidaSeRechazaYNoCambiaElEstado(
        InvoiceState desde,
        InvoiceState hacia)
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(desde);

        // Act + Assert
        Assert.Throws<InvalidInvoiceTransitionException>(
            () => factura.TransitionTo(hacia, FacturaDePrueba.SinCondiciones()));
        Assert.Equal(desde, factura.State);
    }

    // RF-NEG-05: Cancelled es terminal.
    [Fact]
    public void AnuladaNoTieneSalidas()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Cancelled);

        // Act + Assert
        foreach (var destino in Enum.GetValues<InvoiceState>())
        {
            Assert.False(
                InvoiceStateMachine.CanTransition(InvoiceState.Cancelled, destino),
                $"Anulada no deberia poder transicionar a {destino}.");
            Assert.Throws<InvalidInvoiceTransitionException>(
                () => factura.TransitionTo(destino, FacturaDePrueba.SinCondiciones()));
        }

        Assert.Equal(InvoiceState.Cancelled, factura.State);
    }

    // RF-NEG-05: NotDelivered es terminal.
    [Fact]
    public void NoEntregadaNoTieneSalidas()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.NotDelivered);

        // Act + Assert
        foreach (var destino in Enum.GetValues<InvoiceState>())
        {
            Assert.False(
                InvoiceStateMachine.CanTransition(InvoiceState.NotDelivered, destino),
                $"No entregada no deberia poder transicionar a {destino}.");
            Assert.Throws<InvalidInvoiceTransitionException>(
                () => factura.TransitionTo(destino, FacturaDePrueba.SinCondiciones()));
        }

        Assert.Equal(InvoiceState.NotDelivered, factura.State);
    }

    // RF-NEG-03: los siete estados estan declarados en un solo lugar.
    [Fact]
    public void LaFacturaDeclaraSieteEstados()
    {
        // Arrange + Act
        var estados = Enum.GetValues<InvoiceState>();

        // Assert
        Assert.Equal(7, estados.Length);
        Assert.Equal(7, estados.Distinct().Count());
    }

    // La tabla permite exactamente 11 de los 49 pares. Una transicion agregada
    // por accidente cambia el conteo y esta prueba avisa, ademas de la fila
    // concreta que la prueba anterior ya rechaza.
    [Fact]
    public void LaTablaPermiteExactamenteOnceDeLosCuarentaYNuevePares()
    {
        // Arrange
        var estados = Enum.GetValues<InvoiceState>();

        // Act
        var permitidas = estados
            .SelectMany(desde => estados.Select(hacia => (desde, hacia)))
            .Count(par => InvoiceStateMachine.CanTransition(par.desde, par.hacia));

        // Assert
        Assert.Equal(49, estados.Length * estados.Length);
        Assert.Equal(11, permitidas);
    }
}
