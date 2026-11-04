namespace PowerLinesWeb.Analysis;

public class BettingOptions
{
    // MinEdge remains a probability-disagreement diagnostic, never an EV floor.
    public decimal MinExpectedReturn { get; set; } = 0;
    public decimal MaxStakeFraction { get; set; } = .05m;
    public int MaxQuoteAgeMinutes { get; set; } = 30;
    public void Validate()
    {
        if (MinExpectedReturn < 0 || KellyFraction <= 0 || KellyFraction > 1 || MaxStakeFraction <= 0 || MaxStakeFraction > 1 || MinOdds <= 1 || MaxOdds < MinOdds || MaxQuoteAgeMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MinExpectedReturn));
    }
    public decimal MinEdge { get; set; } = 0.05m;
    public decimal KellyFraction { get; set; } = 0.25m;
    public decimal MinOdds { get; set; } = 1.2m;
    public decimal MaxOdds { get; set; } = 10;
}
