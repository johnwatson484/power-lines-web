namespace PowerLinesWeb.Fixtures;

public class FeedOptions
{
    public int MaxImportAgeHours { get; set; } = 48;
    public int MaxResultAgeDaysInSeason { get; set; } = 21;
    public int[] SeasonBreakMonths { get; set; } = [6, 7, 8];
}
