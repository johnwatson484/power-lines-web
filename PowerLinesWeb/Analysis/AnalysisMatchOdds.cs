namespace PowerLinesWeb.Analysis;

public class AnalysisMatchOdds(int id)
{
    public int Id { get; set; } = id;
    public decimal Home { get; set; }
    public decimal Draw { get; set; }
    public decimal Away { get; set; }
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public decimal ExpectedGoals { get; set; }
    public string Recommended { get; set; } = "X";
    public string LowerRecommended { get; set; } = "X";
    public DateTime Calculated { get; set; } = DateTime.UtcNow;
}
