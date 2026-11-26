namespace PirryLedger.Core.Reports.Domain;

public sealed record ReportPeriod(DateOnly From, DateOnly To)
{
    public (DateTime FromUtc, DateTime UntilUtc) ToUtc()
    {
        if (From > To || To == DateOnly.MaxValue)
            throw new ArgumentException("El rango de fechas es inválido.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo");
        return (TimeZoneInfo.ConvertTimeToUtc(From.ToDateTime(TimeOnly.MinValue), zone),
            TimeZoneInfo.ConvertTimeToUtc(To.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
    }
}
