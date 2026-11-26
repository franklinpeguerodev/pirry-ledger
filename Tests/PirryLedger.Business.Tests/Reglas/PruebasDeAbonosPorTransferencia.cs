using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Tests.Reglas;

public sealed class PruebasDeAbonosPorTransferencia
{
    private static readonly DateTime Registro = new(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Usuario = Guid.NewGuid();

    [Fact]
    public void TransferenciaRegistradaNoReduceSaldoYBloqueaMasAbonos()
    {
        var invoice = Deuda();
        invoice.RegisterDebtPayment(500m, Registro, Usuario, PaymentMethod.Transfer);

        Assert.Equal(0m, invoice.PaidAmount);
        Assert.Equal(InvoiceState.InDebt, invoice.State);
        Assert.Throws<InvoiceRuleViolationException>(() => invoice.RegisterDebtPayment(100m, Registro, Usuario));
        Assert.Equal(500m, Assert.Single(invoice.Payments).Amount);
    }

    [Fact]
    public void ConfirmarElAbonoQueSaldaUsaFechaDeRegistroYNoDeConfirmacion()
    {
        var invoice = Deuda();
        invoice.RegisterDebtPayment(1000m, Registro, Usuario, PaymentMethod.Transfer);
        var payment = Assert.Single(invoice.Payments);

        invoice.ConfirmDebtTransfer(payment.Id, Registro.AddDays(2), Usuario);

        Assert.Equal(InvoiceState.Paid, invoice.State);
        Assert.Equal(1000m, invoice.PaidAmount);
        Assert.Equal(Registro, invoice.DebtPaidAtUtc);
        Assert.Equal(Registro, invoice.IssuedAtUtc);
        Assert.Equal(PaymentStatus.Confirmed, payment.Status);
        Assert.Equal(Registro.AddDays(2), payment.ResolvedAtUtc);
    }

    [Fact]
    public void RechazarConMotivoMantieneSaldoYPermiteNuevoAbono()
    {
        var invoice = Deuda();
        invoice.RegisterDebtPayment(500m, Registro, Usuario, PaymentMethod.Transfer);
        var payment = Assert.Single(invoice.Payments);

        invoice.RejectDebtTransfer(payment.Id, Registro.AddDays(1), Usuario, "No llegó");
        invoice.RegisterDebtPayment(100m, Registro.AddDays(1), Usuario);

        Assert.Equal(100m, invoice.PaidAmount);
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Equal("No llegó", payment.RejectionReason);
        Assert.Equal(2, invoice.Payments.Count);
    }

    [Fact]
    public void RechazoSinMotivoNoCambiaElPagoNiElSaldo()
    {
        var invoice = Deuda();
        invoice.RegisterDebtPayment(500m, Registro, Usuario, PaymentMethod.Transfer);
        var payment = Assert.Single(invoice.Payments);

        Assert.Throws<InvoiceRuleViolationException>(() => invoice.RejectDebtTransfer(payment.Id, Registro, Usuario, " "));

        Assert.Equal(PaymentStatus.Registered, payment.Status);
        Assert.Null(payment.RejectionReason);
        Assert.Equal(0m, invoice.PaidAmount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1001)]
    public void AbonoFueraDelSaldoSeRechazaCompletoSinCambios(int amount)
    {
        var invoice = Deuda();

        Assert.Throws<InvoiceRuleViolationException>(() => invoice.RegisterDebtPayment(amount, Registro, Usuario));

        Assert.Equal(InvoiceState.InDebt, invoice.State);
        Assert.Equal(0m, invoice.PaidAmount);
        Assert.Empty(invoice.Payments);
    }

    [Fact]
    public void TransferenciaParcialInicialDeLaDeudaTambienDebeVerificarse()
    {
        var invoice = Invoice.Create();
        invoice.Issue([InvoiceItem.Create("Producto", 1000m, 1)], true, Registro);
        var transfer = InvoicePayment.Transfer(300m, Registro, Usuario);
        invoice.LeaveAsDebt("Cliente", [InvoicePayment.Cash(200m, null, Registro, Usuario), transfer]);

        Assert.Equal(200m, invoice.ConfirmedPaidAmount);
        Assert.Throws<InvoiceRuleViolationException>(() => invoice.RegisterDebtPayment(100m, Registro, Usuario));
        invoice.ConfirmDebtTransfer(transfer.Id, Registro.AddHours(1), Usuario);
        invoice.RegisterDebtPayment(500m, Registro.AddHours(2), Usuario);

        Assert.Equal(InvoiceState.Paid, invoice.State);
        Assert.Equal(1000m, invoice.ConfirmedPaidAmount);
    }

    [Fact]
    public void ResolverElPagoDeOtraFacturaNoModificaNingunaDeLasDos()
    {
        var first = Deuda();
        var second = Deuda();
        first.RegisterDebtPayment(500m, Registro, Usuario, PaymentMethod.Transfer);
        second.RegisterDebtPayment(500m, Registro, Usuario, PaymentMethod.Transfer);
        var foreign = Assert.Single(second.Payments);

        Assert.Throws<InvoiceRuleViolationException>(() => first.ConfirmDebtTransfer(foreign.Id, Registro, Usuario));

        Assert.Equal(0m, first.PaidAmount);
        Assert.Equal(0m, second.PaidAmount);
        Assert.All(first.Payments.Concat(second.Payments), payment => Assert.Equal(PaymentStatus.Registered, payment.Status));
    }

    [Fact]
    public void UnAbonoNuevoSinMetodoValidoNoCambiaLaDeuda()
    {
        var invoice = Deuda();

        Assert.Throws<InvoiceRuleViolationException>(() => invoice.RegisterDebtPayment(100m, Registro, Usuario, PaymentMethod.None));

        Assert.Equal(InvoiceState.InDebt, invoice.State);
        Assert.Empty(invoice.Payments);
    }

    private static Invoice Deuda()
    {
        var invoice = Invoice.Create();
        invoice.Issue([InvoiceItem.Create("Producto", 1000m, 1)], true, Registro);
        invoice.LeaveAsDebt("Cliente", null);
        return invoice;
    }
}
