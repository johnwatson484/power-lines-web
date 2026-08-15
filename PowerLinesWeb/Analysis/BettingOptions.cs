namespace PowerLinesWeb.Analysis;

public class BettingOptions
{
    public decimal MinEdge { get; set; } = 0.05m;
    public decimal KellyFraction { get; set; } = 0.25m;
    public decimal MinOdds { get; set; } = 1.2m;
    public decimal MaxOdds { get; set; } = 10;
}
