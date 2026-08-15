namespace PowerLinesWeb.Analysis;

public static class OddsConverter
{
    // Implied probabilities sum to more than one by the bookmaker's margin, so they are scaled back
    // proportionally. That assumes the margin is spread evenly across the three prices, which is a
    // simplification, but it is the fair comparison a model probability has to beat.
    public static MatchProbabilities RemoveMargin(MarketOdds marketOdds)
    {
        if (!marketOdds.HasPrices)
        {
            return MatchProbabilities.None;
        }

        var home = 1 / marketOdds.Home;
        var draw = 1 / marketOdds.Draw;
        var away = 1 / marketOdds.Away;
        var overround = home + draw + away;

        if (overround <= 0)
        {
            return MatchProbabilities.None;
        }

        return new MatchProbabilities(home / overround, draw / overround, away / overround);
    }
}
