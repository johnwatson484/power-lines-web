namespace PowerLinesWeb.Fixtures;

public class FeedOptions
{
    public string FixtureTimeZoneId { get; set; }
    public int MaxResultAgeDaysInSeason { get; set; } = 21;
    public int[] SeasonBreakMonths { get; set; } = [6, 7, 8];
}
