namespace PowerLinesWeb.Analysis;

// Bookmaker prices for the match, which may be absent because the feed does not always carry them.
public record MarketOdds(decimal Home, decimal Draw, decimal Away)
{
    public string Source { get; init; }
    public string Kind { get; init; } = "LegacyAverageUnknown";
    public string Revision { get; init; }
    public DateTime? ObservedAt { get; init; }
    public bool IsAvailable { get; init; }
    public decimal Commission { get; init; }

    public static MarketOdds None { get; } = new(0, 0, 0);

    // A price of 1.0 or less pays nothing and is a placeholder rather than a quote.
    public bool HasPrices => Home > 1 && Draw > 1 && Away > 1;

    public decimal Get(char result)
    {
        return result switch
        {
            'H' => Home,
            'D' => Draw,
            'A' => Away,
            _ => 0
        };
    }
}
