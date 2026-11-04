namespace PowerLinesWeb.Models;

public class FixturePage
{
    public List<Fixture> Fixtures { get; set; } = [];
    public List<DivisionFeedStatus> Feeds { get; set; } = [];
    public decimal Higher { get; set; }
    public decimal Lower { get; set; }
    public string KickoffTimeZone { get; set; }
    public DateTime AsOf { get; set; }
}

public class DivisionFeedStatus
{
    public string Division { get; set; }
    public DateTime? LastResultDate { get; set; }
    public DateTime? LastFixtureDate { get; set; }
    public DateTime? LastReceivedAt { get; set; }
    public DateTime? LastSuccessfulImport { get; set; }
    public string Status { get; set; }
}
