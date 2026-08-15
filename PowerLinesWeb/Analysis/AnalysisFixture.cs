namespace PowerLinesWeb.Analysis;

public class AnalysisFixture
{
    public int Id { get; set; }
    public string Division { get; set; }
    public DateTime Date { get; set; }
    public string HomeTeam { get; set; }
    public string AwayTeam { get; set; }
    public MarketOdds MarketOdds { get; set; } = MarketOdds.None;
}
