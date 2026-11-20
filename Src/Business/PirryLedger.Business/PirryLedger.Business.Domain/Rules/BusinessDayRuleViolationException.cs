namespace PirryLedger.Business.Domain.Rules;

public sealed class BusinessDayRuleViolationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
