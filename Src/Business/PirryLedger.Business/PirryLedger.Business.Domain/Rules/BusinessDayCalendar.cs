namespace PirryLedger.Business.Domain.Rules;

public static class BusinessDayCalendar
{
    private static readonly TimeZoneInfo BusinessZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo");

    public static DateOnly DateAt(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, BusinessZone));
    }

    public static DateTime IssueDeadline(DateOnly businessDate) =>
        TimeZoneInfo.ConvertTimeToUtc(
            businessDate.AddDays(1).ToDateTime(new TimeOnly(16, 0)),
            BusinessZone);

    public static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("La fecha debe estar en UTC.", "now");
        }
    }
}
