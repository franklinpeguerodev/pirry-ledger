using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Tests.Reglas;

public sealed class PruebasDeVencimientos
{
    private static readonly DateTime Registro = new(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void TransferenciaSoloAvisaDespuesDeCuatroDiasCompletos(int ticks, bool expected)
    {
        var now = Registro.AddDays(4).AddTicks(ticks);

        var due = OverdueBillingRules.IsTransferDue(PaymentMethod.Transfer, PaymentStatus.Registered,
            InvoiceState.InDebt, Registro, now);

        Assert.Equal(expected, due);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, PaymentStatus.Confirmed, InvoiceState.Paid)]
    [InlineData(PaymentMethod.Transfer, PaymentStatus.Confirmed, InvoiceState.Paid)]
    [InlineData(PaymentMethod.Transfer, PaymentStatus.Rejected, InvoiceState.InDebt)]
    [InlineData(PaymentMethod.Transfer, PaymentStatus.Registered, InvoiceState.Cancelled)]
    public void PagoResueltoEfectivoOFacturaAnuladaNoAvisa(PaymentMethod method, PaymentStatus status, InvoiceState state)
    {
        var due = OverdueBillingRules.IsTransferDue(method, status, state, Registro, Registro.AddDays(5));

        Assert.False(due);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void JornadaAbiertaVenceEnElLimiteDeLasCuatroDeRd(int ticks, bool expected)
    {
        var date = new DateOnly(2026, 11, 24);
        var deadline = new DateTime(2026, 11, 25, 20, 0, 0, DateTimeKind.Utc);

        var due = OverdueBillingRules.IsBusinessDayDue(true, date, deadline.AddTicks(ticks));

        Assert.Equal(expected, due);
    }

    [Fact]
    public void JornadaCerradaNoAvisaAunqueHayaSuperadoElLimite()
    {
        Assert.False(OverdueBillingRules.IsBusinessDayDue(false, new DateOnly(2026, 11, 20), Registro.AddDays(5)));
    }

    [Fact]
    public void FechaSinUtcSeRechaza()
    {
        Assert.Throws<ArgumentException>(() => OverdueBillingRules.IsTransferDue(PaymentMethod.Transfer,
            PaymentStatus.Registered, InvoiceState.InDebt, Registro, DateTime.SpecifyKind(Registro, DateTimeKind.Unspecified)));
    }
}
