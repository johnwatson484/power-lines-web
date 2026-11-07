namespace PowerLinesWeb.Analysis;

public class AnalysisMatchOdds(int id)
{
    public int Id { get; set; } = id;
    public decimal Home { get; set; }
    public decimal Draw { get; set; }
    public decimal Away { get; set; }
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public decimal HomeXg { get; set; }
    public decimal AwayXg { get; set; }
    public decimal ExpectedGoals { get; set; }
    public decimal RawHomeProbability { get; set; }
    public decimal RawDrawProbability { get; set; }
    public decimal RawAwayProbability { get; set; }
    public string Support { get; set; } = "LegacyVenue";
    public string CalibrationVersion { get; set; } = "identity";
    public DateTime? CalibrationCutoff { get; set; }
    public string PriceReason { get; set; }
    public decimal ValueExpectedReturn { get; set; }
    public decimal HomeProbability { get; set; }
    public decimal DrawProbability { get; set; }
    public decimal AwayProbability { get; set; }
    public string Recommended { get; set; } = "X";
    public string LowerRecommended { get; set; } = "X";
    public string ValueSelection { get; set; } = "X";
    public decimal ValueEdge { get; set; }
    public decimal ValueOdds { get; set; }
    public decimal ValueStake { get; set; }
    public DateTime Calculated { get; set; } = DateTime.UtcNow;
}
