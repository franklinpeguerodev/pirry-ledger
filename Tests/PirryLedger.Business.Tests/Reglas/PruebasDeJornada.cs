using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Tests.Reglas;

public sealed class PruebasDeJornada
{
    private static readonly DateTime Apertura = new(2026, 11, 12, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LaJornadaConservaFechaLocalAunqueCruceMedianoche()
    {
        var day = BusinessDay.Open(Guid.NewGuid(), Apertura);
        day.EnsureCanIssue(new DateTime(2026, 11, 12, 6, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateOnly(2026, 11, 11), day.BusinessDate);
    }

    [Fact]
    public void UnaAperturaNuevaDeMadrugadaPerteneceASuFechaCalendario()
    {
        var day = BusinessDay.Open(Guid.NewGuid(), new DateTime(2026, 11, 12, 5, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateOnly(2026, 11, 12), day.BusinessDate);
        Assert.Equal(new DateTime(2026, 11, 13, 20, 0, 0, DateTimeKind.Utc), day.IssueDeadlineUtc);
    }

    [Fact]
    public void AlLlegarLasCuatroSeBloqueaEmisionSinCerrarAutomaticamente()
    {
        var day = BusinessDay.Open(Guid.NewGuid(), Apertura);
        Assert.Throws<BusinessDayRuleViolationException>(() => day.EnsureCanIssue(day.IssueDeadlineUtc));
        Assert.True(day.IsOpen);
        Assert.Null(day.Closing);
    }

    [Fact]
    public void ReabrirConservaFechaYCierreHastaVolverACerrar()
    {
        var day = BusinessDay.Open(Guid.NewGuid(), Apertura);
        var close = new BusinessDayClosing(100m, 100m, 0m, 0m, 0m, 0m, 0m, 0m);
        day.Close(close, Apertura.AddHours(2), Guid.NewGuid());
        day.Reopen(Apertura.AddHours(3), hasLaterDay: false);
        Assert.True(day.IsOpen);
        Assert.True(day.IsReopened);
        Assert.Equal(close, day.Closing);
        Assert.Equal(new DateOnly(2026, 11, 11), day.BusinessDate);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ReaperturaFueraDeCondicionesNoModificaElCierre(bool later, bool expired)
    {
        var day = BusinessDay.Open(Guid.NewGuid(), Apertura);
        var close = new BusinessDayClosing(100m, 100m, 0m, 0m, 0m, 0m, 0m, 0m);
        day.Close(close, Apertura.AddHours(2), Guid.NewGuid());
        var closedAt = day.LastClosedAtUtc;
        Assert.Throws<BusinessDayRuleViolationException>(() => day.Reopen(
            expired ? day.IssueDeadlineUtc : Apertura.AddHours(3), later));
        Assert.False(day.IsOpen);
        Assert.Equal(closedAt, day.LastClosedAtUtc);
        Assert.Equal(close, day.Closing);
    }
}
