namespace PowerLinesWeb.Analysis;

public class AnalysisFixture
{
    // Explicit replay cutoff; live caller supplies creation time to prevent future-fixture leakage.
    public DateTime? InformationCutoff { get; set; }
    public bool HistoricalReplay { get; set; }
    public int Id { get; set; }
    public string Division { get; set; }
    public DateTime Date { get; set; }
    public string HomeTeam { get; set; }
    public string AwayTeam { get; set; }
    public MarketOdds MarketOdds { get; set; } = MarketOdds.None;
}
