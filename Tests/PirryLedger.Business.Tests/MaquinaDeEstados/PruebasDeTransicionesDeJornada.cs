using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.StateMachines;

namespace PirryLedger.Business.Tests.MaquinaDeEstados;

public sealed class PruebasDeTransicionesDeJornada
{
    [Theory]
    [InlineData(BusinessDayState.Open, BusinessDayState.Closed, true)]
    [InlineData(BusinessDayState.Closed, BusinessDayState.Open, true)]
    [InlineData(BusinessDayState.Open, BusinessDayState.Open, false)]
    [InlineData(BusinessDayState.Closed, BusinessDayState.Closed, false)]
    public void LaTablaDeclaraSoloCerrarYReabrir(BusinessDayState from, BusinessDayState to, bool expected)
    {
        var allowed = BusinessDayStateMachine.CanTransition(from, to);

        Assert.Equal(expected, allowed);
    }
}
