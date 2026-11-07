namespace PowerLinesWeb.Analysis;

public record WinDecision(string Selection, string Tier, decimal Probability, decimal? FairOdds, string Reason);
public record PriceDecision(string Selection, decimal ExpectedReturn, decimal MarketDisagreement, decimal Price, decimal Stake, string Reason)
{
    public static PriceDecision Unavailable(string reason) => new("X", 0, 0, 0, 0, reason);
}

public static class RecommendationPolicy
{
    public static WinDecision Win(MatchProbabilities p, ThresholdOptions options, bool supported = true)
    {
        options.Validate();
        if (!p.IsValid) return new("X", "None", 0, null, "Invalid probabilities");
        if (!supported) return new("X", "None", 0, null, "Insufficient recommendation support");
        var selection = p.Home > p.Away && p.Home > p.Draw ? "H" : p.Away > p.Home && p.Away > p.Draw ? "A" : "X";
        if (selection == "X") return new("X", "None", 0, null, "No unique home/away winner above draw");
        var probability = p.Get(selection[0]);
        var tier = probability >= options.Higher ? "Strong" : probability >= options.Lower ? "Broader" : "None";
        return new(tier == "None" ? "X" : selection, tier, probability, 1 / probability, tier == "None" ? "Below shortlist threshold" : "Win probability");
    }

    public static PriceDecision Price(MatchProbabilities p, MarketOdds quote, BettingOptions options, DateTime asOf, bool supported = true)
    {
        options.Validate();
        if (!p.IsValid || !supported) return PriceDecision.Unavailable("Unsupported forecast");
        if (quote.Commission < 0 || quote.Commission >= 1) return PriceDecision.Unavailable("Invalid commission");
        if (!quote.HasPrices) return PriceDecision.Unavailable("Price unavailable");
        if (quote.Kind != "AvailableOffer" || string.IsNullOrWhiteSpace(quote.Source) || string.IsNullOrWhiteSpace(quote.Revision) || !quote.IsAvailable || !quote.ObservedAt.HasValue)
            return PriceDecision.Unavailable("Indicative market comparison; no verified available offer");
        var age = asOf - quote.ObservedAt.Value;
        if (age < TimeSpan.Zero || age > TimeSpan.FromMinutes(options.MaxQuoteAgeMinutes)) return PriceDecision.Unavailable("Stale or future quote");
        var market = OddsConverter.RemoveMargin(quote);
        var candidates = new List<PriceDecision>();
        foreach (var side in new[] { 'H', 'A' })
        {
            var price = quote.Get(side);
            if (price < options.MinOdds || price > options.MaxOdds) continue;
            var netPrice = 1 + (price - 1) * (1 - quote.Commission);
            var ev = p.Get(side) * netPrice - 1;
            if (ev <= options.MinExpectedReturn) continue;
            var stake = Math.Min(options.MaxStakeFraction, ev / (netPrice - 1) * options.KellyFraction);
            if (stake <= 0) continue;
            candidates.Add(new(side.ToString(), ev, p.Get(side) - market.Get(side), price, stake, "Available-price positive EV"));
        }
        var ranked = candidates.OrderByDescending(x => x.ExpectedReturn).ToList();
        if (ranked.Count == 0) return PriceDecision.Unavailable("No positive EV above configured floor");
        if (ranked.Count > 1 && ranked[0].ExpectedReturn == ranked[1].ExpectedReturn) return PriceDecision.Unavailable("Equal EV; abstained");
        return ranked[0];
    }
}
