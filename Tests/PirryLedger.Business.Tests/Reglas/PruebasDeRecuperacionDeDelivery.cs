using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Tests.Reglas;

public sealed class PruebasDeRecuperacionDeDelivery
{
    private static readonly DateTime Emision = new(2026, 11, 19, 22, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(InvoiceState.Paid)]
    [InlineData(InvoiceState.PendingVerification)]
    public void NoEntregadaAdmiteLasDosSalidasDeRecuperacion(InvoiceState destino)
    {
        var permitido = InvoiceStateMachine.CanTransition(InvoiceState.NotDelivered, destino);

        Assert.True(permitido);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecuperacionConservaFacturaProductosYJornadaOriginal(bool transferencia)
    {
        var invoice = NoEntregada();
        var id = invoice.Id;
        var day = invoice.BusinessDayId;
        var item = Assert.Single(invoice.Items);
        var payment = transferencia ? InvoicePayment.Transfer(500m, Emision.AddDays(1), Guid.NewGuid())
            : InvoicePayment.Cash(500m, null, Emision.AddDays(1), Guid.NewGuid());

        invoice.RecoverUndelivered([payment]);

        Assert.Equal(transferencia ? InvoiceState.PendingVerification : InvoiceState.Paid, invoice.State);
        Assert.Equal(id, invoice.Id);
        Assert.Equal(day, invoice.BusinessDayId);
        Assert.Equal(Emision, invoice.IssuedAtUtc);
        Assert.Same(item, Assert.Single(invoice.Items));
        Assert.Equal(500m, invoice.Total);
        Assert.Same(payment, Assert.Single(invoice.Payments));
    }

    [Fact]
    public void RecuperadaPorTransferenciaSoloQuedaPagadaAlConfirmar()
    {
        var invoice = NoEntregada();
        invoice.RecoverUndelivered([InvoicePayment.Transfer(500m, Emision.AddDays(1), Guid.NewGuid())]);

        invoice.ConfirmTransfer();

        Assert.Equal(InvoiceState.Paid, invoice.State);
        Assert.Equal(PaymentStatus.Confirmed, Assert.Single(invoice.Payments).Status);
    }

    [Fact]
    public void CobroOrdinarioNoPermiteRecuperarUnaNoEntregada()
    {
        var invoice = NoEntregada();

        Assert.Throws<InvoiceRuleViolationException>(() => invoice.Collect(
            [InvoicePayment.Cash(500m, null, Emision, Guid.NewGuid())]));

        Assert.Equal(InvoiceState.NotDelivered, invoice.State);
        Assert.Empty(invoice.Payments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(400)]
    [InlineData(600)]
    public void RecuperacionEnEfectivoIncompletaOExcesivaNoCambiaFactura(int amount)
    {
        var invoice = NoEntregada();

        var payments = amount == 0 ? Array.Empty<InvoicePayment>() :
            [InvoicePayment.Cash(amount, null, Emision, Guid.NewGuid())];
        if (amount > 500)
            Assert.Throws<InvoiceRuleViolationException>(() => invoice.RecoverUndelivered(payments));
        else
            Assert.Throws<InvoiceConditionNotMetException>(() => invoice.RecoverUndelivered(payments));

        Assert.Equal(InvoiceState.NotDelivered, invoice.State);
        Assert.Empty(invoice.Payments);
    }

    [Fact]
    public void RecuperarDosVecesNoDuplicaPagos()
    {
        var invoice = NoEntregada();
        invoice.RecoverUndelivered([InvoicePayment.Cash(500m, null, Emision, Guid.NewGuid())]);

        Assert.Throws<InvoiceRuleViolationException>(() => invoice.RecoverUndelivered(
            [InvoicePayment.Cash(500m, null, Emision, Guid.NewGuid())]));

        Assert.Equal(InvoiceState.Paid, invoice.State);
        Assert.Single(invoice.Payments);
    }

    [Theory]
    [InlineData(InvoiceState.Paid, false, 500, false)]
    [InlineData(InvoiceState.Paid, true, 400, false)]
    [InlineData(InvoiceState.Paid, true, 500, true)]
    [InlineData(InvoiceState.PendingVerification, false, 500, true)]
    [InlineData(InvoiceState.PendingVerification, true, 500, false)]
    public void CadaCondicionDeLaRecuperacionSeCompruebaEnLaMaquina(InvoiceState destino,
        bool delivery, decimal pagado, bool transferencia)
    {
        var context = new InvoiceTransitionContext
        {
            IsDelivery = delivery, TotalAmount = 500m, PaidAmount = pagado, HasTransfer = transferencia
        };

        Assert.Throws<InvoiceConditionNotMetException>(() =>
            InvoiceStateMachine.Transition(InvoiceState.NotDelivered, destino, context));
    }

    private static Invoice NoEntregada()
    {
        var invoice = Invoice.Create();
        invoice.Issue([InvoiceItem.Create("Combo", 500m, 1)], true, Emision);
        invoice.AssignBusinessDay(Guid.NewGuid());
        invoice.MarkAsNotDelivered();
        return invoice;
    }
}
