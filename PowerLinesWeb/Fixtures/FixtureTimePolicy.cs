namespace PowerLinesWeb.Fixtures;

public static class FixtureTimePolicy
{
    // The legacy timestamp has no offset. A verified source timezone is required before calling a
    // forecast prematch; neither the host timezone nor the user's locale establishes that contract.
    public static DateTime? KickoffUtc(DateTime sourceTime, string timeZoneId)
    {
        if (sourceTime.TimeOfDay == TimeSpan.Zero || string.IsNullOrWhiteSpace(timeZoneId)) return null;
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var local = DateTime.SpecifyKind(sourceTime, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) return null;
            return TimeZoneInfo.ConvertTimeToUtc(local, zone);
        }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }
}
