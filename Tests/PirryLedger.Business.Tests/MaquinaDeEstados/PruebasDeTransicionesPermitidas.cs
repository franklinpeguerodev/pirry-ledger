using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Tests.MaquinaDeEstados;

// RF-NEG-03, RF-NEG-04 y RF-NEG-05: una prueba por cada fila de la tabla de
// transiciones permitidas de docs/maquina-de-estados.md, mas las pruebas de las
// condiciones que cada fila declara. Cada rechazo comprueba tambien que el
// estado no cambio.
public sealed class PruebasDeTransicionesPermitidas
{
    // Fila 1: Draft -> Issued. Quien cobra; la factura tiene al menos un item.
    [Fact]
    public void EmitirUnaFacturaConItemsEsValido()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Draft);

        // Act
        factura.TransitionTo(InvoiceState.Issued, FacturaDePrueba.Emitir());

        // Assert
        Assert.Equal(InvoiceState.Issued, factura.State);
    }

    // Fila 2: Issued -> Paid. Los pagos cubren el total y ninguno es transferencia.
    [Fact]
    public void CobrarEnEfectivoConPagoCompletoEsValido()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Issued);

        // Act
        factura.TransitionTo(InvoiceState.Paid, FacturaDePrueba.CobrarSinTransferencia());

        // Assert
        Assert.Equal(InvoiceState.Paid, factura.State);
    }

    // Fila 3: Issued -> PendingVerification. Algun pago es transferencia.
    [Fact]
    public void CobrarConTransferenciaPasaAVerificacionPendiente()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Issued);

        // Act
        factura.TransitionTo(InvoiceState.PendingVerification, FacturaDePrueba.CobrarConTransferencia());

        // Assert
        Assert.Equal(InvoiceState.PendingVerification, factura.State);
    }

    // Fila 4: Issued -> InDebt. Exige el nombre de la persona.
    [Fact]
    public void DejarComoDeudaConNombreEsValido()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Issued);

        // Act
        factura.TransitionTo(InvoiceState.InDebt, FacturaDePrueba.DejarComoDeuda());

        // Assert
        Assert.Equal(InvoiceState.InDebt, factura.State);
    }

    // Fila 5: Issued -> Cancelled. Sin permiso: la factura aun no se cobro.
    [Fact]
    public void AnularUnaFacturaEmitidaEsValido()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Issued);

        // Act
        factura.TransitionTo(InvoiceState.Cancelled, FacturaDePrueba.SinCondiciones());

        // Assert
        Assert.Equal(InvoiceState.Cancelled, factura.State);
    }

    // Fila 6: Issued -> NotDelivered. La factura es de delivery.
    [Fact]
    public void MarcarNoEntregadaUnaFacturaDeDeliveryEsValido()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Issued);

        // Act
        factura.TransitionTo(InvoiceState.NotDelivered, FacturaDePrueba.EsDelivery());

        // Assert
        Assert.Equal(InvoiceState.NotDelivered, factura.State);
    }

    // Fila 7: PendingVerification -> Paid. El Administrador confirma que la
    // transferencia llego.
    [Fact]
    public void ConfirmarLaTransferenciaPasaAPagada()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.PendingVerification);

        // Act
        factura.TransitionTo(InvoiceState.Paid, FacturaDePrueba.SinCondiciones());

        // Assert
        Assert.Equal(InvoiceState.Paid, factura.State);
    }

    // Fila 8: PendingVerification -> InDebt. Transferencia rechazada; exige el
    // nombre de la persona.
    [Fact]
    public void RechazarLaTransferenciaPasaADeuda()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.PendingVerification);

        // Act
        factura.TransitionTo(InvoiceState.InDebt, FacturaDePrueba.DejarComoDeuda());

        // Assert
        Assert.Equal(InvoiceState.InDebt, factura.State);
    }

    // Fila 9: InDebt -> Paid. Los abonos cubren el total de la deuda.
    [Fact]
    public void AbonarLaDeudaCompletaPasaAPagada()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.InDebt);

        // Act
        factura.TransitionTo(InvoiceState.Paid, FacturaDePrueba.CobrarSinTransferencia());

        // Assert
        Assert.Equal(InvoiceState.Paid, factura.State);
    }

    // Fila 10: Paid -> Cancelled. Existe un permiso aprobado para esa factura;
    // la maquina no lo consulta (esa linea vive en el caso de uso).
    [Fact]
    public void AnularUnaFacturaPagadaEsValido()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Paid);

        // Act
        factura.TransitionTo(InvoiceState.Cancelled, FacturaDePrueba.SinCondiciones());

        // Assert
        Assert.Equal(InvoiceState.Cancelled, factura.State);
    }

    // Fila 11: InDebt -> Cancelled. Igual que la anterior.
    [Fact]
    public void AnularUnaFacturaEnDeudaEsValido()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.InDebt);

        // Act
        factura.TransitionTo(InvoiceState.Cancelled, FacturaDePrueba.SinCondiciones());

        // Assert
        Assert.Equal(InvoiceState.Cancelled, factura.State);
    }

    // Condicion de la fila 1. Es la verificacion que el defecto 3 elimina: si
    // desaparece, esta prueba se pone roja.
    [Fact]
    public void EmitirSinItemsSeRechazaYNoCambiaElEstado()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Draft);
        var sinItems = new InvoiceTransitionContext { ItemCount = 0 };

        // Act + Assert
        Assert.Throws<InvoiceConditionNotMetException>(
            () => factura.TransitionTo(InvoiceState.Issued, sinItems));
        Assert.Equal(InvoiceState.Draft, factura.State);
    }

    // Condicion de la fila 2: los pagos no cubren el total.
    [Fact]
    public void CobrarSinCubrirElTotalSeRechazaYNoCambiaElEstado()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Issued);
        var pagoIncompleto = new InvoiceTransitionContext
        {
            TotalAmount = FacturaDePrueba.Total,
            PaidAmount = FacturaDePrueba.Total - 1m,
        };

        // Act + Assert
        Assert.Throws<InvoiceConditionNotMetException>(
            () => factura.TransitionTo(InvoiceState.Paid, pagoIncompleto));
        Assert.Equal(InvoiceState.Issued, factura.State);
    }

    // Condicion de la fila 2: una transferencia no se marca pagada sin
    // verificarla antes.
    [Fact]
    public void CobrarConTransferenciaNoSeMarcaPagadaYNoCambiaElEstado()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Issued);

        // Act + Assert
        Assert.Throws<InvoiceConditionNotMetException>(
            () => factura.TransitionTo(InvoiceState.Paid, FacturaDePrueba.CobrarConTransferencia()));
        Assert.Equal(InvoiceState.Issued, factura.State);
    }

    // Condicion de la fila 3: sin transferencia no hay nada que verificar.
    [Fact]
    public void PedirVerificacionSinTransferenciaSeRechazaYNoCambiaElEstado()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Issued);

        // Act + Assert
        Assert.Throws<InvoiceConditionNotMetException>(
            () => factura.TransitionTo(InvoiceState.PendingVerification, FacturaDePrueba.CobrarSinTransferencia()));
        Assert.Equal(InvoiceState.Issued, factura.State);
    }

    // Condicion de la fila 4: la deuda exige el nombre de la persona.
    [Fact]
    public void DejarComoDeudaSinNombreSeRechazaYNoCambiaElEstado()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Issued);
        var sinNombre = new InvoiceTransitionContext { DebtorName = "   " };

        // Act + Assert
        Assert.Throws<InvoiceConditionNotMetException>(
            () => factura.TransitionTo(InvoiceState.InDebt, sinNombre));
        Assert.Equal(InvoiceState.Issued, factura.State);
    }

    // Condicion de la fila 8: tambien al rechazar una transferencia.
    [Fact]
    public void RechazarLaTransferenciaSinNombreSeRechazaYNoCambiaElEstado()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.PendingVerification);
        var sinNombre = new InvoiceTransitionContext { DebtorName = null };

        // Act + Assert
        Assert.Throws<InvoiceConditionNotMetException>(
            () => factura.TransitionTo(InvoiceState.InDebt, sinNombre));
        Assert.Equal(InvoiceState.PendingVerification, factura.State);
    }

    // Condicion de la fila 6: solo una factura de delivery puede quedar sin
    // entregar.
    [Fact]
    public void MarcarNoEntregadaUnaFacturaQueNoEsDeliverySeRechazaYNoCambiaElEstado()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.Issued);

        // Act + Assert
        Assert.Throws<InvoiceConditionNotMetException>(
            () => factura.TransitionTo(InvoiceState.NotDelivered, FacturaDePrueba.SinCondiciones()));
        Assert.Equal(InvoiceState.Issued, factura.State);
    }

    // Condicion de la fila 9: los abonos tienen que cubrir la deuda entera.
    [Fact]
    public void AbonarSinCubrirLaDeudaSeRechazaYNoCambiaElEstado()
    {
        // Arrange
        var factura = FacturaDePrueba.EnEstado(InvoiceState.InDebt);
        var abonoParcial = new InvoiceTransitionContext
        {
            TotalAmount = FacturaDePrueba.Total,
            PaidAmount = FacturaDePrueba.Total / 2m,
        };

        // Act + Assert
        Assert.Throws<InvoiceConditionNotMetException>(
            () => factura.TransitionTo(InvoiceState.Paid, abonoParcial));
        Assert.Equal(InvoiceState.InDebt, factura.State);
    }

    // RF-NEG-09: la maquina de la factura no comparte codigo ni depende de la
    // de permisos. La prueba de la otra direccion ya existe en la suite de
    // Permissions; esta comprueba el sentido que le toca al negocio.
    [Fact]
    public void LaMaquinaDeLaFacturaNoDependeDeLaDePermisos()
    {
        // Arrange
        var referencias = typeof(InvoiceStateMachine).Assembly
            .GetReferencedAssemblies()
            .Select(referencia => referencia.Name ?? string.Empty)
            .ToList();

        // Act + Assert
        Assert.DoesNotContain(referencias, nombre => nombre.Contains("Permissions"));
        Assert.DoesNotContain(referencias, nombre => nombre.Contains("AccessControl"));
    }
}
